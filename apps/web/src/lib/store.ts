"use client";

import { create } from "zustand";
import { User } from "@/types";

interface AppState {
  userId: string;
  currentUser: User | null;
  token: string | null;
  currentWorkspaceId: string | null;
  currentConversationId: string | null;
  activeTraceId: string | null;

  // 모델 및 Ollama 연동 상태
  selectedModelType: "local" | "server";
  localOllamaEndpoint: string;
  localOllamaModel: string;
  serverModel: string;
  isProSubscriber: boolean;

  setUserId: (id: string) => void;
  login: (user: User, token: string) => void;
  logout: () => void;
  setCurrentWorkspaceId: (id: string | null) => void;
  setCurrentConversationId: (id: string | null) => void;
  setActiveTraceId: (id: string | null) => void;

  setSelectedModelType: (type: "local" | "server") => void;
  setLocalOllamaEndpoint: (endpoint: string) => void;
  setLocalOllamaModel: (model: string) => void;
  setIsProSubscriber: (isPro: boolean) => void;
}

const DEFAULT_USER_ID = "00000000-0000-0000-0000-000000000001";
const DEFAULT_OLLAMA_ENDPOINT = "http://localhost:11434";
const DEFAULT_OLLAMA_MODEL = "qwen2.5:7b";
const DEFAULT_SERVER_MODEL = "Qwen 2.5 32B (클라우드 고성능 GPU)";

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

function getInitialLocalEndpoint(): string {
  if (typeof window === "undefined") return DEFAULT_OLLAMA_ENDPOINT;
  return localStorage.getItem("dograc_ollama_endpoint") || DEFAULT_OLLAMA_ENDPOINT;
}

function getInitialLocalModel(): string {
  if (typeof window === "undefined") return DEFAULT_OLLAMA_MODEL;
  return localStorage.getItem("dograc_ollama_model") || DEFAULT_OLLAMA_MODEL;
}

function getInitialModelType(): "local" | "server" {
  if (typeof window === "undefined") return "local";
  return (localStorage.getItem("dograc_model_type") as "local" | "server") || "local";
}

function getInitialProStatus(): boolean {
  if (typeof window === "undefined") return false;
  return localStorage.getItem("dograc_is_pro") === "true";
}

export const useAppStore = create<AppState>((set) => ({
  userId: getInitialUserId(),
  currentUser: getInitialUser(),
  token: getInitialToken(),
  currentWorkspaceId: null,
  currentConversationId: null,
  activeTraceId: null,

  // 모델 관련 초기값
  selectedModelType: getInitialModelType(),
  localOllamaEndpoint: getInitialLocalEndpoint(),
  localOllamaModel: getInitialLocalModel(),
  serverModel: DEFAULT_SERVER_MODEL,
  isProSubscriber: getInitialProStatus(),

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

  setSelectedModelType: (type) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_model_type", type);
    }
    set({ selectedModelType: type });
  },

  setLocalOllamaEndpoint: (endpoint) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_ollama_endpoint", endpoint);
    }
    set({ localOllamaEndpoint: endpoint });
  },

  setLocalOllamaModel: (model) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_ollama_model", model);
    }
    set({ localOllamaModel: model });
  },

  setIsProSubscriber: (isPro) => {
    if (typeof window !== "undefined") {
      localStorage.setItem("dograc_is_pro", String(isPro));
    }
    set({ isProSubscriber: isPro });
  },
}));
