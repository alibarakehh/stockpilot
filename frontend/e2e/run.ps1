$ErrorActionPreference = 'Stop'

$databaseName = 'StockPilotE2E'
$serverName = '(localdb)\MSSQLLocalDB'
$frontendRoot = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path (Split-Path -Parent $frontendRoot) 'backend\InventoryApi.csproj'
$connectionString = "Server=$serverName;Database=$databaseName;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
$resetSql = @"
IF DB_ID(N'$databaseName') IS NOT NULL
BEGIN
    ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$databaseName];
END
"@

function Reset-E2EDatabase {
    if ($databaseName -ne 'StockPilotE2E') {
        throw 'Refusing to reset any database except StockPilotE2E.'
    }

    & sqlcmd -S $serverName -E -d master -b -Q $resetSql
    if ($LASTEXITCODE -ne 0) {
        throw "Could not reset the disposable $databaseName database."
    }
}

& SqlLocalDB start MSSQLLocalDB | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not start SQL Server LocalDB.'
}

Reset-E2EDatabase
$env:E2E_CONNECTION_STRING = $connectionString
$env:ConnectionStrings__DefaultConnection = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:E2E_DEMO_PASSWORD = "E2e!$([Guid]::NewGuid().ToString('N'))aA1"
$env:SeedDemoPassword = $env:E2E_DEMO_PASSWORD

& dotnet run --project $backendProject --configuration Release --no-launch-profile -- --migrate
if ($LASTEXITCODE -ne 0) {
    Reset-E2EDatabase
    throw 'The controlled E2E database migration command failed.'
}
$migrationVerificationSql = @"
IF OBJECT_ID(N'dbo.DataProtectionKeys', N'U') IS NULL
    THROW 51000, 'The latest deployment migration was not applied.', 1;
IF EXISTS (SELECT 1 FROM dbo.Workspaces)
    THROW 51001, 'The migration-only command unexpectedly seeded demo data.', 1;
"@
& sqlcmd -S $serverName -E -d $databaseName -b -Q $migrationVerificationSql
if ($LASTEXITCODE -ne 0) {
    Reset-E2EDatabase
    throw 'The controlled E2E migration verification failed.'
}

$testExitCode = 1

try {
    Push-Location $frontendRoot
    try {
        & npx --no-install playwright test @args
        $testExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    Reset-E2EDatabase
}

exit $testExitCode
