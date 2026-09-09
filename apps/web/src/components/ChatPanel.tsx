"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import {
  Plus,
  Send,
  Trash2,
  Loader2,
  FileText,
  SearchCode,
  MessageSquare,
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
  const { data: conversationDetail } = useQuery({
    queryKey: ["conversation", userId, currentConversationId],
    queryFn: () =>
      currentConversationId
        ? api.getConversation(userId, currentConversationId)
        : null,
    enabled: Boolean(currentConversationId),
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
    // [파일명, p.숫자] 패턴을 자연스럽게 스타일링
    const parts = content.split(/(\[[^\]]+,\s*p\.\d+\])/g);
    return parts.map((part, index) => {
      if (part.startsWith("[") && part.endsWith("]")) {
        return (
          <span
            key={index}
            className="inline-flex items-center px-1.5 py-0.5 mx-0.5 rounded text-[11px] font-medium bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200"
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
      <div className="h-full flex flex-col items-center justify-center p-8 text-center bg-white">
        <p className="text-xs font-semibold text-kds-gray-800">대화 세션 준비 완료</p>
        <p className="text-[11px] text-kds-gray-500 mt-1">
          워크스페이스를 선택하고 문서에 관한 질문을 시작해 보세요.
        </p>
      </div>
    );
  }

  const messages = conversationDetail?.messages || [];

  return (
    <div className="h-full flex flex-col bg-kds-gray-50 overflow-hidden min-h-0">
      {/* 상단 대화 세션 탭 (KDS Reading Tab Pattern) */}
      <div className="h-12 bg-white border-b border-kds-gray-300 px-4 flex items-center justify-between flex-shrink-0">
        <div className="flex items-center space-x-1.5 overflow-x-auto min-w-0 pr-2">
          {conversations.map((conv) => {
            const isActive = conv.id === currentConversationId;
            return (
              <div
                key={conv.id}
                onClick={() => setCurrentConversationId(conv.id)}
                className={`group flex items-center space-x-2 px-3 py-1.5 rounded text-xs font-medium cursor-pointer transition-colors ${
                  isActive
                    ? "bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200"
                    : "text-kds-gray-600 hover:text-kds-gray-900 hover:bg-kds-gray-50 border border-transparent"
                }`}
              >
                <span className="truncate max-w-[120px]">{conv.title}</span>
                {isActive && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (confirm("이 대화를 삭제하시겠습니까?")) {
                        deleteConversationMutation.mutate(conv.id);
                      }
                    }}
                    className="opacity-0 group-hover:opacity-100 text-kds-gray-400 hover:text-kds-red-500 transition-opacity p-0.5"
                    title="대화 삭제"
                  >
                    <Trash2 className="w-3 h-3 stroke-[1.5]" />
                  </button>
                )}
              </div>
            );
          })}
        </div>

        <button
          onClick={() => createConversationMutation.mutate()}
          className="h-7 px-2.5 rounded border border-kds-gray-300 bg-white hover:bg-kds-gray-50 text-xs font-medium text-kds-gray-700 transition-colors flex-shrink-0"
        >
          + 새 대화
        </button>
      </div>

      {/* 메시지 영역 (Reading-First Layout) */}
      <div className="flex-1 min-h-0 overflow-y-auto px-5 sm:px-6 py-6 space-y-5">
        {messages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-center max-w-sm mx-auto p-4">
            <h3 className="text-sm font-bold text-kds-gray-900">
              문서 기반 질의응답
            </h3>
            <p className="text-xs text-kds-gray-600 mt-1.5 leading-relaxed">
              업로드된 문서의 문맥만을 바탕으로 정확한 근거와 함께 답변합니다.
              문서에 없는 내용은 추측하지 않습니다.
            </p>
          </div>
        ) : (
          messages.map((msg) => {
            const isUser = msg.role === "user";
            return (
              <div
                key={msg.id}
                className={`flex flex-col ${isUser ? "items-end" : "items-start"}`}
              >
                {/* 발화자 레이블 */}
                <span className="text-[11px] font-medium text-kds-gray-500 mb-1 px-1">
                  {isUser ? "사용자" : "dograc"}
                </span>

                {/* 메시지 버블 */}
                <div
                  className={`max-w-[85%] sm:max-w-[75%] rounded-lg px-4 py-3 text-xs leading-relaxed border ${
                    isUser
                      ? "bg-kds-blue-50/80 border-kds-blue-200 text-kds-gray-900 rounded-tr-none font-normal"
                      : "bg-white border-kds-gray-300 text-kds-gray-900 rounded-tl-none font-normal shadow-xs"
                  }`}
                >
                  <div className="whitespace-pre-wrap">
                    {isUser ? msg.content : renderContentWithCitations(msg.content)}
                  </div>

                  {/* 어시스턴트 메시지 하단 Trace 액션 */}
                  {!isUser && msg.trace_id && (
                    <div className="mt-2.5 pt-2 border-t border-kds-gray-200 flex items-center justify-end">
                      <button
                        onClick={() => setActiveTraceId(msg.trace_id!)}
                        className="text-[11px] font-medium text-kds-blue-700 hover:text-kds-blue-800 hover:underline transition-colors"
                      >
                        실행 Trace 분석 보기 →
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })
        )}

        {sendMessageMutation.isPending && (
          <div className="flex flex-col items-start space-y-1">
            <span className="text-[11px] font-medium text-kds-gray-500 px-1">dograc</span>
            <div className="bg-white border border-kds-gray-300 rounded rounded-tl-none px-4 py-3 shadow-subtle flex items-center space-x-2 text-xs text-kds-gray-700">
              <Loader2 className="w-4 h-4 text-kds-blue-600 animate-spin" strokeWidth={2} />
              <span>문서를 검색하고 답변을 생성하고 있습니다...</span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* 하단 입력 영역 (KDS Input Box) */}
      <div className="p-3.5 sm:p-4 bg-white border-t border-kds-gray-300 flex-shrink-0">
        <form onSubmit={handleSend} className="max-w-[960px] mx-auto flex items-center space-x-2">
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
            placeholder="문서 내용에 대해 질문하세요..."
            disabled={sendMessageMutation.isPending}
            className="flex-1 h-10 border border-kds-gray-300 rounded px-3.5 text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 disabled:bg-kds-gray-50 transition-colors"
          />
          <button
            type="submit"
            disabled={!inputContent.trim() || sendMessageMutation.isPending}
            className="h-10 px-4 bg-kds-blue-600 hover:bg-kds-blue-800 text-white rounded text-xs font-semibold inline-flex items-center space-x-1.5 transition-colors disabled:opacity-40 flex-shrink-0"
          >
            <span>전송</span>
            <Send className="w-3.5 h-3.5 stroke-[1.5]" />
          </button>
        </form>
      </div>
    </div>
  );
}
