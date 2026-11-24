export interface ChatMsg {
  id: string
  text: string
  at: string
  fromVisitor: boolean
  authorName: string | null
}

export interface ChatQuestion {
  id: string
  text: string
}
