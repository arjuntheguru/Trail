import { useCallback, useEffect, useRef, useState } from 'react'

import { ApiError } from '@/api/problem'

export type SaveStatus = 'idle' | 'unsaved' | 'saving' | 'saved' | 'offline' | 'error'

export const AUTOSAVE_DELAY_MS = 1500

interface AutosaveOptions {
  /** False when the task cannot be worked on; nothing is saved then. */
  enabled: boolean
  /** The serialized draft; a save is scheduled whenever it differs from the last saved one. */
  snapshot: string
  /** The snapshot the server already holds (nothing to save until the draft changes). */
  initialSnapshot: string
  save: (snapshot: string) => Promise<void>
  delayMs?: number
}

/**
 * Debounced draft saving: ~1.5 s after the last change, immediately on blur, when the tab is hidden or the
 * connection returns. Saves never overlap; a change made during a save is saved right after it.
 */
export function useAutosave({
  enabled,
  snapshot,
  initialSnapshot,
  save,
  delayMs = AUTOSAVE_DELAY_MS,
}: AutosaveOptions) {
  const [status, setStatus] = useState<SaveStatus>('idle')
  const lastSaved = useRef(initialSnapshot)
  const latest = useRef(snapshot)
  const enabledRef = useRef(enabled)
  const saveRef = useRef(save)
  const inflight = useRef(false)
  const cancelled = useRef(false)
  const flushRef = useRef<() => Promise<void>>(async () => {})
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => {
    enabledRef.current = enabled && !cancelled.current
    saveRef.current = save
  })

  const flush = useCallback(async () => {
    clearTimeout(timer.current)
    if (!enabledRef.current || inflight.current) return
    if (latest.current === lastSaved.current) return
    if (typeof navigator !== 'undefined' && navigator.onLine === false) {
      setStatus('offline')
      return
    }
    const attempt = latest.current
    inflight.current = true
    setStatus('saving')
    let succeeded = false
    try {
      await saveRef.current(attempt)
      lastSaved.current = attempt
      succeeded = true
      setStatus(latest.current === attempt ? 'saved' : 'unsaved')
    } catch (error) {
      setStatus(error instanceof ApiError && error.status === 0 ? 'offline' : 'error')
    } finally {
      inflight.current = false
    }
    // Edits made while saving go out right away rather than waiting for another change.
    if (succeeded && latest.current !== lastSaved.current) void flushRef.current()
  }, [])

  useEffect(() => {
    flushRef.current = flush
  }, [flush])

  useEffect(() => {
    latest.current = snapshot
    if (!enabled || snapshot === lastSaved.current) return
    setStatus((s) => (s === 'saving' ? s : 'unsaved'))
    timer.current = setTimeout(() => void flush(), delayMs)
    return () => clearTimeout(timer.current)
  }, [snapshot, enabled, delayMs, flush])

  useEffect(() => {
    const onVisibility = () => {
      if (document.visibilityState === 'hidden') void flush()
    }
    const onOnline = () => void flush()
    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('pagehide', onOnline)
    window.addEventListener('online', onOnline)
    return () => {
      document.removeEventListener('visibilitychange', onVisibility)
      window.removeEventListener('pagehide', onOnline)
      window.removeEventListener('online', onOnline)
      void flush() // leaving the screen with unsaved changes
    }
  }, [flush])

  /** Drops any scheduled save, e.g. when the task is being completed with the same answers. */
  const cancel = useCallback(() => {
    clearTimeout(timer.current)
    cancelled.current = true
    enabledRef.current = false
  }, [])

  const resume = useCallback(() => {
    cancelled.current = false
  }, [])

  return { status, flush, cancel, resume }
}
