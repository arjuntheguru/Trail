import type { ApiClient } from '@/api/client'
import { toApiError } from '@/api/problem'

export type ExportKind = 'tasks' | 'issues' | 'audit'

const PATH = {
  tasks: '/api/v1/reports/tasks.csv',
  issues: '/api/v1/reports/issues.csv',
  audit: '/api/v1/reports/audit.csv',
} as const

/** The file name a Content-Disposition header asks for, or undefined when it has none. */
export function filenameFromDisposition(header: string | null): string | undefined {
  if (!header) return undefined
  const encoded = /filename\*\s*=\s*(?:[\w-]+)?'[^']*'([^;]+)/i.exec(header)?.[1]
  if (encoded) {
    try {
      return decodeURIComponent(encoded.trim())
    } catch {
      // fall through to the plain form
    }
  }
  const plain = /filename\s*=\s*(?:"([^"]+)"|([^;]+))/i.exec(header)
  return (plain?.[1] ?? plain?.[2])?.trim() || undefined
}

export function fallbackFilename(kind: ExportKind, from: string, to: string) {
  return `${kind}-${from}-to-${to}.csv`
}

/** Hands a blob to the browser as a download. The object URL is released right after the click. */
export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.hidden = true
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

export interface ExportParams {
  from: string
  to: string
  siteId?: string
}

/**
 * Fetches a CSV export with the bearer token (a plain link cannot send it) and saves it. Throws an ApiError
 * carrying the API's message when it refuses, for example a range with too many rows.
 */
export async function downloadExport(client: ApiClient, kind: ExportKind, params: ExportParams) {
  const query = { From: params.from, To: params.to, SiteId: params.siteId }
  let result
  try {
    result = await client.GET(PATH[kind], { params: { query }, parseAs: 'blob' })
  } catch {
    throw toApiError(0, undefined)
  }
  if (!result.response.ok) throw toApiError(result.response.status, result.error)
  const name =
    filenameFromDisposition(result.response.headers.get('content-disposition')) ??
    fallbackFilename(kind, params.from, params.to)
  saveBlob(result.data as unknown as Blob, name)
  return name
}
