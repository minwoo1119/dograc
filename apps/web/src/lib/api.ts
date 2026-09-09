import {
  AuthResponse,
  Conversation,
  ConversationDetail,
  DocumentItem,
  Message,
  Trace,
  User,
  Workspace,
} from "@/types";
import { toast } from "@/lib/toast";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8000/api/v1";

async function request<T>(
  path: string,
  userId?: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers || {});
  
  if (userId) {
    headers.set("X-User-ID", userId);
  }

  if (typeof window !== "undefined") {
    const token = localStorage.getItem("dograc_token");
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    let errorMsg = `요청 실패 (상태 코드 ${res.status})`;
    let errorCode: string | undefined = `HTTP_${res.status}`;

    try {
      const errData = await res.json();
      if (errData?.error) {
        if (errData.error.message) errorMsg = errData.error.message;
        if (errData.error.code) errorCode = errData.error.code;
      } else if (errData?.detail) {
        errorMsg = typeof errData.detail === "string" ? errData.detail : JSON.stringify(errData.detail);
      }
    } catch {
      // Ignore JSON parse error
    }

    // 우측 하단 에러 토스트 자동 팝업
    if (typeof window !== "undefined") {
      toast.error(errorMsg, errorCode);
    }

    throw new Error(errorMsg);
  }

  if (res.status === 204) {
    return {} as T;
  }

  return res.json();
}

export const api = {
  // Auth
  register: (email: string, username: string, password: string) =>
    request<AuthResponse>("/auth/register", undefined, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, username, password }),
    }),

  login: (emailOrUsername: string, password: string) =>
    request<AuthResponse>("/auth/login", undefined, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email_or_username: emailOrUsername, password }),
    }),

  getMe: (userId: string) =>
    request<User>("/auth/me", userId),

  logout: () =>
    request<{ message: string }>("/auth/logout", undefined, {
      method: "POST",
    }),

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

    if (typeof window !== "undefined") {
      const token = localStorage.getItem("dograc_token");
      if (token) {
        headers.set("Authorization", `Bearer ${token}`);
      }
    }

    const res = await fetch(
      `${BASE_URL}/workspaces/${workspaceId}/documents`,
      {
        method: "POST",
        headers,
        body: formData,
      }
    );

    if (!res.ok) {
      let errorMsg = `업로드 실패 (${res.status})`;
      let errorCode: string | undefined = `HTTP_${res.status}`;
      try {
        const err = await res.json();
        if (err?.error) {
          if (err.error.message) errorMsg = err.error.message;
          if (err.error.code) errorCode = err.error.code;
        } else if (err?.detail) {
          errorMsg = typeof err.detail === "string" ? err.detail : JSON.stringify(err.detail);
        }
      } catch {
        // Ignore
      }
      if (typeof window !== "undefined") {
        toast.error(errorMsg, errorCode);
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
