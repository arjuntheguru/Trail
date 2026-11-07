import { useCallback, useState } from 'react'

/** Counts uploads in flight so a form can hold its submit button until they finish. */
export function useUploadTracker() {
  const [pending, setPending] = useState(0)
  const track = useCallback(<T>(upload: Promise<T>) => {
    setPending((n) => n + 1)
    return upload.finally(() => setPending((n) => n - 1))
  }, [])
  return { pending, track }
}
