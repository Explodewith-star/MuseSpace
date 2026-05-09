export type ExportFormat = 'md' | 'txt'

export interface ExportChaptersOptions {
  storyOutlineId?: string
  format?: ExportFormat
  from?: number
  to?: number
  onlyFinal?: boolean
  includeDraft?: boolean
}

/**
 * 触发章节聚合导出，下载到本地。
 * 走原生 fetch，避免被全局 Axios 响应拦截器（ApiResponse 解包）影响 blob 下载。
 */
export async function exportProjectChapters(
  projectId: string,
  options: ExportChaptersOptions = {},
): Promise<void> {
  const params = new URLSearchParams()
  params.set('format', options.format ?? 'md')
  if (options.storyOutlineId) params.set('storyOutlineId', options.storyOutlineId)
  if (options.from != null) params.set('from', String(options.from))
  if (options.to != null) params.set('to', String(options.to))
  params.set('onlyFinal', String(options.onlyFinal ?? true))
  params.set('includeDraft', String(options.includeDraft ?? false))

  const baseURL = import.meta.env.VITE_APP_BASE_API || '/api'
  const token = localStorage.getItem('musespace_token')

  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`

  const resp = await fetch(`${baseURL}/projects/${projectId}/export?${params.toString()}`, {
    method: 'GET',
    headers,
  })

  if (!resp.ok) {
    let message = `导出失败（${resp.status}）`
    try {
      const text = await resp.text()
      const body = JSON.parse(text)
      if (body && typeof body.errorMessage === 'string') message = body.errorMessage
    } catch {
      // ignore
    }
    throw new Error(message)
  }

  const blob = await resp.blob()
  const dispo = resp.headers.get('content-disposition') ?? ''
  const filename = parseFileName(dispo) ?? buildFallbackName(options.format ?? 'md')

  const url = window.URL.createObjectURL(blob)
  try {
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  } finally {
    window.URL.revokeObjectURL(url)
  }
}

function parseFileName(contentDisposition: string): string | null {
  // 优先 RFC 5987：filename*=UTF-8''xxx
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)
  if (utf8Match) {
    try {
      return decodeURIComponent(utf8Match[1])
    } catch {
      // fall through
    }
  }
  const m = /filename="?([^";]+)"?/.exec(contentDisposition)
  if (m) {
    try {
      return decodeURIComponent(m[1])
    } catch {
      return m[1]
    }
  }
  return null
}

function buildFallbackName(format: ExportFormat): string {
  const ts = new Date().toISOString().slice(0, 16).replace(/[T:]/g, '-')
  return `export_${ts}.${format}`
}
