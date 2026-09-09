import {
  Conversation,
  ConversationDetail,
  DocumentItem,
  Message,
  Trace,
  Workspace,
} from "@/types";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8000/api/v1";

async function request<T>(
  path: string,
  userId: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers || {});
  headers.set("X-User-ID", userId);

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    let errorMsg = `Request failed with status ${res.status}`;
    try {
      const errData = await res.json();
      if (errData?.error?.message) {
        errorMsg = errData.error.message;
      }
    } catch {
      // Ignore JSON parse error
    }
    throw new Error(errorMsg);
  }

  if (res.status === 204) {
    return {} as T;
  }

  return res.json();
}

export const api = {
  // Workspaces
  getWorkspaces: (userId: string) =>
    request<Workspace[]>("/workspaces", userId),

  createWorkspace: (userId: string, name: string) =>
    request<Workspace>("/workspaces", userId, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    }),

  deleteWorkspace: (userId: string, workspaceId: string) =>
    request<void>(`/workspaces/${workspaceId}`, userId, {
      method: "DELETE",
    }),

  // Documents
  getDocuments: (userId: string, workspaceId: string) =>
    request<DocumentItem[]>(`/workspaces/${workspaceId}/documents`, userId),

  uploadDocument: async (
    userId: string,
    workspaceId: string,
    file: File
  ): Promise<DocumentItem> => {
    const formData = new FormData();
    formData.append("file", file);

    const headers = new Headers();
    headers.set("X-User-ID", userId);

    const res = await fetch(
      `${BASE_URL}/workspaces/${workspaceId}/documents`,
      {
        method: "POST",
        headers,
        body: formData,
      }
    );

    if (!res.ok) {
      let errorMsg = `Upload failed (${res.status})`;
      try {
        const err = await res.json();
        if (err?.error?.message) errorMsg = err.error.message;
      } catch {
        // Ignore
      }
      throw new Error(errorMsg);
    }

    return res.json();
  },

  processDocument: (userId: string, documentId: string) =>
    request<DocumentItem>(`/documents/${documentId}/process`, userId, {
      method: "POST",
    }),

  deleteDocument: (userId: string, documentId: string) =>
    request<void>(`/documents/${documentId}`, userId, {
      method: "DELETE",
    }),

  // Conversations
  getConversations: (userId: string, workspaceId: string) =>
    request<Conversation[]>(`/workspaces/${workspaceId}/conversations`, userId),

  createConversation: (
    userId: string,
    workspaceId: string,
    title?: string
  ) =>
    request<Conversation>(`/workspaces/${workspaceId}/conversations`, userId, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title }),
    }),

  getConversation: (userId: string, conversationId: string) =>
    request<ConversationDetail>(`/conversations/${conversationId}`, userId),

  deleteConversation: (userId: string, conversationId: string) =>
    request<void>(`/conversations/${conversationId}`, userId, {
      method: "DELETE",
    }),

  sendMessage: (userId: string, conversationId: string, content: string) =>
    request<Message>(`/conversations/${conversationId}/messages`, userId, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ content }),
    }),

  // Trace
  getTrace: (userId: string, traceId: string) =>
    request<Trace>(`/traces/${traceId}`, userId),
};
