// 对齐后端 MuseSpace.Contracts.Common.ApiResponse<T>
export interface ApiResponse<T> {
  success: boolean
  data?: T
  errorMessage?: string
  requestId?: string
}
