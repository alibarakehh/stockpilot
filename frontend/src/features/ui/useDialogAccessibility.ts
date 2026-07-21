import { useEffect, useRef, type RefObject } from 'react'

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

export function useDialogAccessibility<T extends HTMLElement>(
  onEscape: () => void,
): RefObject<T | null> {
  const dialog = useRef<T>(null)
  const escapeHandler = useRef(onEscape)

  useEffect(() => {
    escapeHandler.current = onEscape
  }, [onEscape])

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const focusFrame = window.requestAnimationFrame(() => {
      if (dialog.current?.contains(document.activeElement)) return
      const initialFocus = dialog.current?.querySelector<HTMLElement>('[data-dialog-initial-focus]')
      if (initialFocus) initialFocus.focus()
      else focusableElements(dialog.current)[0]?.focus()
    })

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        escapeHandler.current()
        return
      }
      if (event.key !== 'Tab') return

      const elements = focusableElements(dialog.current)
      if (!elements.length) {
        event.preventDefault()
        return
      }

      const first = elements[0]
      const last = elements[elements.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => {
      window.cancelAnimationFrame(focusFrame)
      window.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = previousOverflow
      previouslyFocused?.focus()
    }
  }, [])

  return dialog
}

function focusableElements(container: HTMLElement | null): HTMLElement[] {
  return container
    ? Array.from(container.querySelectorAll<HTMLElement>(focusableSelector)).filter(
        (element) => element.getAttribute('aria-hidden') !== 'true',
      )
    : []
}
