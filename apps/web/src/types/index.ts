export interface User {
  id: string;
  email: string;
  username: string;
  created_at: string;
}

export interface AuthResponse {
  access_token: string;
  token_type: string;
  user: User;
}

export interface Workspace {
  id: string;
  name: string;
  owner_id: string;
  created_at: string;
  updated_at: string;
}

export type DocumentStatus = "uploaded" | "processing" | "ready" | "failed";

export interface DocumentItem {
  id: string;
  workspace_id: string;
  source_file_name: string;
  media_type: string;
  status: DocumentStatus;
  failure_code?: string | null;
  created_at: string;
  updated_at: string;
}

export type MessageRole = "user" | "assistant";

export interface Message {
  id: string;
  conversation_id: string;
  role: MessageRole;
  content: string;
  created_at: string;
  trace_id?: string | null;
}

export interface Conversation {
  id: string;
  workspace_id: string;
  title: string;
  created_at: string;
  updated_at: string;
}

export interface ConversationDetail extends Conversation {
  messages: Message[];
}

export interface RetrievedChunk {
  chunk_id: string;
  document_id: string;
  document_version_id: string;
  source_file_name: string;
  page_number: number;
  section_title?: string | null;
  chunk_index: number;
  text: string;
  score: number;
}

export interface Trace {
  id: string;
  message_id: string;
  workspace_id: string;
  retrieved_chunks: RetrievedChunk[];
  prompt?: string | null;
  model_name?: string | null;
  latency_ms?: number | null;
  created_at: string;
}
