import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'

interface TaskFormContextValue {
  siteId: string
  /** Set when photos may be queued offline against this task. */
  taskId?: string
  /** True while the task cannot be edited (completed, not yet open, or the user may not complete it). */
  readOnly: boolean
  /** Number of uploads in flight; completion waits for them. */
  pendingUploads: number
  trackUpload: <T>(upload: Promise<T>) => Promise<T>
}

const TaskFormContext = createContext<TaskFormContextValue | null>(null)

export function TaskFormProvider({
  siteId,
  taskId,
  readOnly,
  children,
}: {
  siteId: string
  /** Set when photos may be queued offline against this task. */
  taskId?: string
  readOnly: boolean
  children: ReactNode
}) {
  const [pendingUploads, setPending] = useState(0)
  const trackUpload = useCallback(<T,>(upload: Promise<T>) => {
    setPending((n) => n + 1)
    return upload.finally(() => setPending((n) => n - 1))
  }, [])
  const value = useMemo(
    () => ({ siteId, taskId, readOnly, pendingUploads, trackUpload }),
    [siteId, taskId, readOnly, pendingUploads, trackUpload],
  )
  return <TaskFormContext.Provider value={value}>{children}</TaskFormContext.Provider>
}

export function useTaskForm() {
  const context = useContext(TaskFormContext)
  if (!context) throw new Error('useTaskForm must be used inside <TaskFormProvider>.')
  return context
}
