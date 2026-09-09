import { create } from "zustand";

interface AppState {
  userId: string;
  currentWorkspaceId: string | null;
  currentConversationId: string | null;
  activeTraceId: string | null;
  setUserId: (id: string) => void;
  setCurrentWorkspaceId: (id: string | null) => void;
  setCurrentConversationId: (id: string | null) => void;
  setActiveTraceId: (id: string | null) => void;
}

const DEFAULT_USER_ID = "00000000-0000-0000-0000-000000000001";

export const useAppStore = create<AppState>((set) => ({
  userId: typeof window !== "undefined"
    ? localStorage.getItem("dograc_user_id") || DEFAULT_USER_ID
    : DEFAULT_USER_ID,
  currentWorkspaceId: null,
  currentConversationId: null,
  activeTraceId: null,
  setUserId: (id: string) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_user_id", id);
    }
    set({ userId: id });
  },
  setCurrentWorkspaceId: (id) => set({ currentWorkspaceId: id, currentConversationId: null }),
  setCurrentConversationId: (id) => set({ currentConversationId: id }),
  setActiveTraceId: (id) => set({ activeTraceId: id }),
}));
