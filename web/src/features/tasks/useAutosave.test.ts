import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ApiError } from '@/api/problem'

import { AUTOSAVE_DELAY_MS, useAutosave } from './useAutosave'

type Props = { snapshot: string; enabled?: boolean }

function setup(save = vi.fn().mockResolvedValue(undefined), initial = '{"a":0}') {
  const hook = renderHook(
    ({ snapshot, enabled = true }: Props) =>
      useAutosave({ enabled, snapshot, initialSnapshot: initial, save }),
    { initialProps: { snapshot: initial } as Props },
  )
  return { ...hook, save }
}

beforeEach(() => vi.useFakeTimers())
afterEach(() => vi.useRealTimers())

describe('useAutosave', () => {
  it('saves nothing until the draft changes', () => {
    const { save } = setup()
    act(() => void vi.advanceTimersByTime(10_000))
    expect(save).not.toHaveBeenCalled()
  })

  it('debounces: one save with the latest draft 1.5 s after the last change', async () => {
    const { result, rerender, save } = setup()
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS - 100))
    rerender({ snapshot: '{"a":2}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS - 100))
    expect(save).not.toHaveBeenCalled()
    expect(result.current.status).toBe('unsaved')

    await act(async () => void vi.advanceTimersByTime(100))
    expect(save).toHaveBeenCalledTimes(1)
    expect(save).toHaveBeenCalledWith('{"a":2}')
    expect(result.current.status).toBe('saved')
  })

  it('saves right away on flush (blur) and when the tab is hidden', async () => {
    const { result, rerender, save } = setup()
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void result.current.flush())
    expect(save).toHaveBeenLastCalledWith('{"a":1}')

    rerender({ snapshot: '{"a":2}' })
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true })
    await act(async () => void document.dispatchEvent(new Event('visibilitychange')))
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
    expect(save).toHaveBeenLastCalledWith('{"a":2}')
    expect(save).toHaveBeenCalledTimes(2)
  })

  it('does not save when disabled', async () => {
    const { result, rerender, save } = setup()
    rerender({ snapshot: '{"a":1}', enabled: false })
    await act(async () => void vi.advanceTimersByTime(10_000))
    await act(async () => void result.current.flush())
    expect(save).not.toHaveBeenCalled()
  })

  it('reports offline for network failures and retries when the connection returns', async () => {
    const save = vi
      .fn()
      .mockRejectedValueOnce(new ApiError({ status: 0 }))
      .mockResolvedValue(undefined)
    const { result, rerender } = setup(save)
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    expect(result.current.status).toBe('offline')

    await act(async () => void window.dispatchEvent(new Event('online')))
    expect(save).toHaveBeenCalledTimes(2)
    expect(result.current.status).toBe('saved')
  })

  it('keeps the draft on the device when offline and a queue is available', async () => {
    const save = vi.fn().mockResolvedValue(undefined)
    const saveOffline = vi.fn().mockResolvedValue(undefined)
    const initial = '{"a":0}'
    const { result, rerender } = renderHook(
      ({ snapshot }: Props) =>
        useAutosave({ enabled: true, snapshot, initialSnapshot: initial, save, saveOffline }),
      { initialProps: { snapshot: initial } as Props },
    )
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    expect(saveOffline).toHaveBeenCalledWith('{"a":1}')
    expect(save).not.toHaveBeenCalled()
    expect(result.current.status).toBe('queued')
    vi.restoreAllMocks()
  })

  it('queues the draft when the request cannot reach the server', async () => {
    const save = vi.fn().mockRejectedValue(new ApiError({ status: 0 }))
    const saveOffline = vi.fn().mockResolvedValue(undefined)
    const initial = '{"a":0}'
    const { result, rerender } = renderHook(
      ({ snapshot }: Props) =>
        useAutosave({ enabled: true, snapshot, initialSnapshot: initial, save, saveOffline }),
      { initialProps: { snapshot: initial } as Props },
    )
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    expect(saveOffline).toHaveBeenCalledWith('{"a":1}')
    expect(result.current.status).toBe('queued')
  })

  it('reports other failures as errors', async () => {
    const save = vi.fn().mockRejectedValue(new ApiError({ status: 500 }))
    const { result, rerender } = setup(save)
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    expect(result.current.status).toBe('error')
  })

  it('saves edits made during a save straight after it', async () => {
    let release!: () => void
    const save = vi
      .fn()
      .mockImplementationOnce(() => new Promise<void>((resolve) => (release = resolve)))
      .mockResolvedValue(undefined)
    const { rerender } = setup(save)
    rerender({ snapshot: '{"a":1}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    rerender({ snapshot: '{"a":2}' })
    await act(async () => release())
    expect(save).toHaveBeenCalledTimes(2)
    expect(save).toHaveBeenLastCalledWith('{"a":2}')
  })

  it('cancel stops pending saves and resume allows them again', async () => {
    const { result, rerender, save } = setup()
    rerender({ snapshot: '{"a":1}' })
    act(() => result.current.cancel())
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS * 2))
    expect(save).not.toHaveBeenCalled()

    act(() => result.current.resume())
    rerender({ snapshot: '{"a":2}' })
    await act(async () => void vi.advanceTimersByTime(AUTOSAVE_DELAY_MS))
    expect(save).toHaveBeenCalledWith('{"a":2}')
  })
})
