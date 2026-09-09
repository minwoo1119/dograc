"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import {
  Bot,
  MessageSquarePlus,
  Send,
  Sparkles,
  User,
  Activity,
  Trash2,
  Loader2,
} from "lucide-react";

export function ChatPanel() {
  const queryClient = useQueryClient();
  const {
    userId,
    currentWorkspaceId,
    currentConversationId,
    setCurrentConversationId,
    setActiveTraceId,
  } = useAppStore();

  const [inputContent, setInputContent] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // 워크스페이스 내 대화 세션 목록
  const { data: conversations = [] } = useQuery({
    queryKey: ["conversations", userId, currentWorkspaceId],
    queryFn: () =>
      currentWorkspaceId
        ? api.getConversations(userId, currentWorkspaceId)
        : [],
    enabled: Boolean(currentWorkspaceId),
  });

  // 활성 대화 세션의 상세(메시지 목록)
  const { data: conversationDetail, isLoading: isLoadingMessages } = useQuery({
    queryKey: ["conversation", userId, currentConversationId],
    queryFn: () =>
      currentConversationId
        ? api.getConversation(userId, currentConversationId)
        : null,
    enabled: Boolean(currentConversationId),
    refetchInterval: false,
  });

  // 첫 번째 대화 자동 선택
  useEffect(() => {
    if (!currentConversationId && conversations.length > 0) {
      setCurrentConversationId(conversations[0].id);
    }
  }, [conversations, currentConversationId, setCurrentConversationId]);

  // 스크롤 맨 아래로 이동
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversationDetail?.messages]);

  // 대화 생성
  const createConversationMutation = useMutation({
    mutationFn: () => {
      if (!currentWorkspaceId) throw new Error("워크스페이스가 필요합니다.");
      return api.createConversation(userId, currentWorkspaceId);
    },
    onSuccess: (newConv) => {
      queryClient.invalidateQueries({
        queryKey: ["conversations", userId, currentWorkspaceId],
      });
      setCurrentConversationId(newConv.id);
    },
  });

  // 대화 삭제
  const deleteConversationMutation = useMutation({
    mutationFn: (convId: string) => api.deleteConversation(userId, convId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["conversations", userId, currentWorkspaceId],
      });
      setCurrentConversationId(null);
    },
  });

  // 메시지 전송
  const sendMessageMutation = useMutation({
    mutationFn: (content: string) => {
      if (!currentConversationId) throw new Error("대화 세션이 없습니다.");
      return api.sendMessage(userId, currentConversationId, content);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["conversation", userId, currentConversationId],
      });
      setInputContent("");
    },
  });

  const handleSend = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!inputContent.trim() || sendMessageMutation.isPending) return;

    // 만약 현재 대화가 없으면 대화 먼저 생성 후 전송
    if (!currentConversationId && currentWorkspaceId) {
      createConversationMutation.mutate(undefined, {
        onSuccess: (newConv) => {
          api
            .sendMessage(userId, newConv.id, inputContent.trim())
            .then(() => {
              queryClient.invalidateQueries({
                queryKey: ["conversation", userId, newConv.id],
              });
              setInputContent("");
            });
        },
      });
      return;
    }

    sendMessageMutation.mutate(inputContent.trim());
  };

  const renderContentWithCitations = (content: string) => {
    // [파일명, p.숫자] 패턴 매칭
    const parts = content.split(/(\[[^\]]+,\s*p\.\d+\])/g);
    return parts.map((part, index) => {
      if (part.startsWith("[") && part.endsWith("]")) {
        return (
          <span
            key={index}
            className="inline-block bg-indigo-100 text-indigo-800 text-[11px] font-semibold px-1.5 py-0.2 rounded border border-indigo-200 mx-0.5"
          >
            {part}
          </span>
        );
      }
      return part;
    });
  };

  if (!currentWorkspaceId) {
    return (
      <div className="h-full flex items-center justify-center p-6 text-center text-slate-400">
        <div>
          <Bot className="w-12 h-12 mx-auto mb-2 opacity-30 text-indigo-400" />
          <p className="text-base font-semibold text-slate-600">dograc RAG 질의응답</p>
          <p className="text-xs text-slate-400 mt-1">
            워크스페이스를 선택하면 문서 기반 질의응답을 시작할 수 있습니다.
          </p>
        </div>
      </div>
    );
  }

  const messages = conversationDetail?.messages || [];

  return (
    <div className="h-full flex flex-col bg-slate-50">
      {/* 상단 대화 세션 탭 */}
      <div className="bg-white border-b border-slate-200 px-4 py-2.5 flex items-center justify-between">
        <div className="flex items-center space-x-2 overflow-x-auto">
          {conversations.map((conv) => {
            const isActive = conv.id === currentConversationId;
            return (
              <div
                key={conv.id}
                onClick={() => setCurrentConversationId(conv.id)}
                className={`group flex items-center space-x-2 px-3 py-1.5 rounded-lg text-xs font-medium cursor-pointer transition ${
                  isActive
                    ? "bg-indigo-50 text-indigo-700 border border-indigo-200"
                    : "text-slate-600 hover:bg-slate-100"
                }`}
              >
                <span>{conv.title}</span>
                {isActive && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (confirm("이 대화를 삭제하시겠습니까?")) {
                        deleteConversationMutation.mutate(conv.id);
                      }
                    }}
                    className="opacity-0 group-hover:opacity-100 text-slate-400 hover:text-red-500 transition"
                  >
                    <Trash2 className="w-3 h-3" />
                  </button>
                )}
              </div>
            );
          })}
        </div>

        <button
          onClick={() => createConversationMutation.mutate()}
          className="flex items-center space-x-1 text-xs font-semibold text-indigo-600 hover:text-indigo-700 hover:bg-indigo-50 px-2.5 py-1.5 rounded-lg transition flex-shrink-0"
        >
          <MessageSquarePlus className="w-4 h-4" />
          <span>새 대화</span>
        </button>
      </div>

      {/* 메시지 영역 */}
      <div className="flex-1 overflow-y-auto p-4 sm:p-6 space-y-4">
        {messages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-center text-slate-400">
            <Sparkles className="w-10 h-10 text-indigo-400 mb-2 opacity-60" />
            <p className="text-sm font-semibold text-slate-700">
              업로드된 문서를 바탕으로 질문해 보세요
            </p>
            <p className="text-xs text-slate-400 max-w-sm mt-1">
              근거 문맥을 검색하고, 정확한 출처(파일명, 페이지)와 함께 답변을 생성합니다.
            </p>
          </div>
        ) : (
          messages.map((msg) => {
            const isUser = msg.role === "user";
            return (
              <div
                key={msg.id}
                className={`flex items-start space-x-3 ${
                  isUser ? "flex-row-reverse space-x-reverse" : ""
                }`}
              >
                <div
                  className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 text-white ${
                    isUser ? "bg-slate-700" : "bg-indigo-600"
                  }`}
                >
                  {isUser ? <User className="w-4 h-4" /> : <Bot className="w-4 h-4" />}
                </div>

                <div className={`max-w-[80%] space-y-1`}>
                  <div
                    className={`rounded-2xl px-4 py-2.5 text-sm shadow-sm whitespace-pre-wrap ${
                      isUser
                        ? "bg-slate-800 text-white rounded-tr-none"
                        : "bg-white text-slate-800 border border-slate-200 rounded-tl-none leading-relaxed"
                    }`}
                  >
                    {isUser ? msg.content : renderContentWithCitations(msg.content)}
                  </div>

                  {!isUser && msg.trace_id && (
                    <div className="flex items-center justify-start pl-1">
                      <button
                        onClick={() => setActiveTraceId(msg.trace_id!)}
                        className="flex items-center space-x-1 text-[11px] text-slate-400 hover:text-indigo-600 transition"
                      >
                        <Activity className="w-3 h-3" />
                        <span>RAG Trace 확인</span>
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })
        )}

        {sendMessageMutation.isPending && (
          <div className="flex items-start space-x-3">
            <div className="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center text-white flex-shrink-0">
              <Bot className="w-4 h-4" />
            </div>
            <div className="bg-white border border-slate-200 rounded-2xl rounded-tl-none px-4 py-3 shadow-sm flex items-center space-x-2 text-xs text-slate-500">
              <Loader2 className="w-4 h-4 animate-spin text-indigo-600" />
              <span>관련 문서를 검색하고 답변을 생성하고 있습니다...</span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* 입력 영역 */}
      <div className="p-4 bg-white border-t border-slate-200">
        <form onSubmit={handleSend} className="flex items-center space-x-2">
          <input
            type="text"
            value={inputContent}
            onChange={(e) => setInputContent(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                handleSend();
              }
            }}
            placeholder="문서에 대해 질문하세요..."
            disabled={sendMessageMutation.isPending}
            className="flex-1 border border-slate-300 rounded-xl px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-slate-50"
          />
          <button
            type="submit"
            disabled={!inputContent.trim() || sendMessageMutation.isPending}
            className="p-2.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl disabled:opacity-40 transition flex-shrink-0"
          >
            <Send className="w-4 h-4" />
          </button>
        </form>
      </div>
    </div>
  );
}
