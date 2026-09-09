import { create } from "zustand";
import { User } from "@/types";

interface AppState {
  userId: string;
  currentUser: User | null;
  token: string | null;
  currentWorkspaceId: string | null;
  currentConversationId: string | null;
  activeTraceId: string | null;
  setUserId: (id: string) => void;
  login: (user: User, token: string) => void;
  logout: () => void;
  setCurrentWorkspaceId: (id: string | null) => void;
  setCurrentConversationId: (id: string | null) => void;
  setActiveTraceId: (id: string | null) => void;
}

const DEFAULT_USER_ID = "00000000-0000-0000-0000-000000000001";

function getInitialUser(): User | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = localStorage.getItem("dograc_user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function getInitialToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem("dograc_token") || null;
}

function getInitialUserId(): string {
  if (typeof window === "undefined") return DEFAULT_USER_ID;
  const user = getInitialUser();
  if (user?.id) return user.id;
  return localStorage.getItem("dograc_user_id") || DEFAULT_USER_ID;
}

export const useAppStore = create<AppState>((set) => ({
  userId: getInitialUserId(),
  currentUser: getInitialUser(),
  token: getInitialToken(),
  currentWorkspaceId: null,
  currentConversationId: null,
  activeTraceId: null,

  setUserId: (id: string) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_user_id", id);
    }
    set({ userId: id });
  },

  login: (user: User, token: string) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_user", JSON.stringify(user));
      localStorage.setItem("dograc_token", token);
      localStorage.setItem("dograc_user_id", user.id);
    }
    set({
      currentUser: user,
      token,
      userId: user.id,
      currentWorkspaceId: null,
      currentConversationId: null,
    });
  },

  logout: () => {
    if (typeof window !== "undefined") {
      localStorage.removeItem("dograc_user");
      localStorage.removeItem("dograc_token");
      localStorage.removeItem("dograc_user_id");
    }
    set({
      currentUser: null,
      token: null,
      userId: DEFAULT_USER_ID,
      currentWorkspaceId: null,
      currentConversationId: null,
      activeTraceId: null,
    });
  },

  setCurrentWorkspaceId: (id) => set({ currentWorkspaceId: id, currentConversationId: null }),
  setCurrentConversationId: (id) => set({ currentConversationId: id }),
  setActiveTraceId: (id) => set({ activeTraceId: id }),
}));
