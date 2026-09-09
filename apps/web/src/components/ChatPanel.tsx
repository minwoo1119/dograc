"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { MarkdownRenderer } from "@/components/MarkdownRenderer";
import { Plus, Trash2 } from "lucide-react";

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
  // 낙관적 UI 상태: 전송 즉시 표시할 사용자 메시지
  const [optimisticUserMessage, setOptimisticUserMessage] = useState<string | null>(null);
  // 응답 실패 상태
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastFailedQuestion, setLastFailedQuestion] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

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
  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [conversationDetail?.messages, optimisticUserMessage, errorMessage]);

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

  // 메시지 전송 Mutation
  const sendMessageMutation = useMutation({
    mutationFn: ({ convId, content }: { convId: string; content: string }) => {
      return api.sendMessage(userId, convId, content);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["conversation", userId, currentConversationId],
      });
      setOptimisticUserMessage(null);
      setErrorMessage(null);
      setLastFailedQuestion(null);
    },
    onError: (err: any) => {
      setErrorMessage(err?.message || "답변을 생성하는 도중 오류가 발생했습니다.");
      setLastFailedQuestion(optimisticUserMessage);
      setOptimisticUserMessage(null);
    },
  });

  const executeSend = (text: string, targetConvId?: string) => {
    const trimmed = text.trim();
    if (!trimmed) return;

    // 즉시 낙관적 사용자 메시지 표시
    setOptimisticUserMessage(trimmed);
    setErrorMessage(null);
    setLastFailedQuestion(null);
    setInputContent("");

    // 텍스트에리어 높이 초기화
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
    }

    const activeConvId = targetConvId || currentConversationId;

    if (!activeConvId && currentWorkspaceId) {
      createConversationMutation.mutate(undefined, {
        onSuccess: (newConv) => {
          sendMessageMutation.mutate({ convId: newConv.id, content: trimmed });
        },
        onError: (err: any) => {
          setErrorMessage("대화 세션 생성에 실패했습니다: " + (err?.message || ""));
          setLastFailedQuestion(trimmed);
          setOptimisticUserMessage(null);
        },
      });
      return;
    }

    if (activeConvId) {
      sendMessageMutation.mutate({ convId: activeConvId, content: trimmed });
    }
  };

  const handleSend = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!inputContent.trim() || sendMessageMutation.isPending || optimisticUserMessage) return;
    executeSend(inputContent);
  };

  // 실패 시 재시도
  const handleRetry = () => {
    if (!lastFailedQuestion) return;
    executeSend(lastFailedQuestion);
  };

  // 실패 질문을 입력창으로 복원
  const handleRestoreInput = () => {
    if (lastFailedQuestion) {
      setInputContent(lastFailedQuestion);
      setErrorMessage(null);
      setLastFailedQuestion(null);
      if (textareaRef.current) {
        textareaRef.current.focus();
      }
    }
  };

  // 마크다운 문법 삽입 헬퍼
  const insertMarkdown = (prefix: string, suffix: string = "") => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const previousText = textarea.value;
    const selectedText = previousText.substring(start, end);

    const replacement = prefix + (selectedText || "텍스트") + suffix;
    const nextText =
      previousText.substring(0, start) + replacement + previousText.substring(end);

    setInputContent(nextText);

    setTimeout(() => {
      textarea.focus();
      const newCursorPos = start + prefix.length + (selectedText ? selectedText.length : 3);
      textarea.setSelectionRange(newCursorPos, newCursorPos);
    }, 0);
  };

  // 텍스트에리어 높이 자동 조절
  const handleTextareaChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInputContent(e.target.value);
    e.target.style.height = "auto";
    e.target.style.height = `${Math.min(e.target.scrollHeight, 160)}px`;
  };

  const isPending = Boolean(optimisticUserMessage || sendMessageMutation.isPending);

  if (!currentWorkspaceId) {
    return (
      <div className="h-full flex flex-col items-center justify-center p-8 text-center bg-white">
        <p className="text-sm font-semibold text-kds-gray-800">대화 세션 준비 완료</p>
        <p className="text-xs text-kds-gray-500 mt-1.5">
          상단에서 워크스페이스를 선택하고 문서에 관한 질문을 시작해 보세요.
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

      {/* 메시지 영역 (Reading-First Layout with Markdown) */}
      <div className="flex-1 min-h-0 overflow-y-auto px-5 sm:px-6 py-6 space-y-5">
        {messages.length === 0 && !optimisticUserMessage ? (
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
          <>
            {/* 서버에 저장된 메시지 목록 */}
            {messages.map((msg) => {
              const isUser = msg.role === "user";
              return (
                <div
                  key={msg.id}
                  className={`flex flex-col ${isUser ? "items-end" : "items-start"}`}
                >
                  <span className="text-[11px] font-medium text-kds-gray-500 mb-1 px-1">
                    {isUser ? "사용자" : "dograc"}
                  </span>

                  <div
                    className={`max-w-[88%] sm:max-w-[80%] rounded-lg px-4 py-3 text-xs sm:text-sm leading-relaxed border ${
                      isUser
                        ? "bg-kds-blue-50/80 border-kds-blue-200 text-kds-gray-900 rounded-tr-none font-normal"
                        : "bg-white border-kds-gray-300 text-kds-gray-900 rounded-tl-none font-normal shadow-xs"
                    }`}
                  >
                    <MarkdownRenderer content={msg.content} isUser={isUser} />

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
            })}

            {/* 낙관적 사용자 메시지 (전송 버튼 누르는 즉시 노출) */}
            {optimisticUserMessage && (
              <div className="flex flex-col items-end">
                <span className="text-[11px] font-medium text-kds-gray-500 mb-1 px-1">
                  사용자
                </span>
                <div className="max-w-[88%] sm:max-w-[80%] rounded-lg px-4 py-3 text-xs sm:text-sm leading-relaxed border bg-kds-blue-50/80 border-kds-blue-200 text-kds-gray-900 rounded-tr-none font-normal">
                  <MarkdownRenderer content={optimisticUserMessage} isUser={true} />
                </div>
              </div>
            )}

            {/* 어시스턴트 응답 대기 UI (타이핑/검색 중 인디케이터) */}
            {isPending && (
              <div className="flex flex-col items-start space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 px-1">dograc</span>
                <div className="bg-white border border-kds-gray-300 rounded-lg rounded-tl-none px-4 py-3 shadow-xs flex items-center space-x-3 text-xs text-kds-gray-700">
                  <span className="flex space-x-1 items-center">
                    <span className="w-1.5 h-1.5 bg-kds-blue-600 rounded-full animate-bounce [animation-delay:-0.3s]" />
                    <span className="w-1.5 h-1.5 bg-kds-blue-600 rounded-full animate-bounce [animation-delay:-0.15s]" />
                    <span className="w-1.5 h-1.5 bg-kds-blue-600 rounded-full animate-bounce" />
                  </span>
                  <span>문서를 검색하고 근거 기반 답변을 작성하고 있습니다...</span>
                </div>
              </div>
            )}

            {/* 응답 실패 시 상세 에러 및 재시도 UI */}
            {errorMessage && (
              <div className="flex flex-col items-start space-y-1">
                <span className="text-[11px] font-medium text-kds-gray-500 px-1">dograc</span>
                <div className="bg-kds-red-50 border border-kds-red-200 rounded-lg rounded-tl-none p-4 shadow-xs text-xs text-kds-red-700 space-y-2.5 max-w-md">
                  <div>
                    <div className="font-semibold text-kds-red-900 flex items-center space-x-1.5">
                      <span>답변 생성에 실패했습니다</span>
                    </div>
                    <p className="mt-1 text-kds-red-800 text-[11px] leading-relaxed break-words">
                      {errorMessage}
                    </p>
                  </div>

                  <div className="flex items-center space-x-2 pt-2 border-t border-kds-red-100">
                    <button
                      onClick={handleRetry}
                      disabled={isPending}
                      className="px-3 py-1.5 bg-kds-red-600 hover:bg-kds-red-700 text-white rounded text-xs font-semibold transition-colors disabled:opacity-50"
                    >
                      다시 시도
                    </button>
                    <button
                      onClick={handleRestoreInput}
                      className="px-3 py-1.5 bg-white hover:bg-kds-red-50 border border-kds-red-300 text-kds-red-700 rounded text-xs font-medium transition-colors"
                    >
                      입력창으로 복원
                    </button>
                  </div>
                </div>
              </div>
            )}
          </>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* 하단 마크다운 지원 멀티라인 입력 영역 */}
      <div className="p-3 sm:p-4 bg-white border-t border-kds-gray-300 flex-shrink-0 space-y-2">
        {/* 가벼운 마크다운 툴바 */}
        <div className="max-w-[960px] mx-auto flex items-center justify-between text-xs text-kds-gray-500">
          <div className="flex items-center space-x-1.5">
            <span className="text-[11px] text-kds-gray-400 font-medium mr-1">마크다운:</span>
            <button
              type="button"
              onClick={() => insertMarkdown("**", "**")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 font-bold text-[11px]"
              title="굵게 (**텍스트**)"
            >
              B
            </button>
            <button
              type="button"
              onClick={() => insertMarkdown("*", "*")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 italic text-[11px]"
              title="기울임 (*텍스트*)"
            >
              I
            </button>
            <button
              type="button"
              onClick={() => insertMarkdown("`", "`")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 font-mono text-[11px]"
              title="인라인 코드 (`코드`)"
            >
              &lt;/&gt;
            </button>
            <button
              type="button"
              onClick={() => insertMarkdown("```\n", "\n```")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 font-mono text-[11px]"
              title="코드 블록"
            >
              [Code]
            </button>
            <button
              type="button"
              onClick={() => insertMarkdown("> ")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 text-[11px]"
              title="인용문 (> 인용)"
            >
              &quot;
            </button>
            <button
              type="button"
              onClick={() => insertMarkdown("- ")}
              className="px-1.5 py-0.5 rounded hover:bg-kds-gray-100 text-kds-gray-700 text-[11px]"
              title="목록 (- 항목)"
            >
              • 목록
            </button>
          </div>

          <div className="hidden sm:block text-[11px] text-kds-gray-400">
            Enter 전송 · Shift+Enter 줄바꿈
          </div>
        </div>

        {/* 텍스트 입력창 및 전송 버튼 */}
        <form onSubmit={handleSend} className="max-w-[960px] mx-auto flex items-end space-x-2">
          <textarea
            ref={textareaRef}
            rows={1}
            value={inputContent}
            onChange={handleTextareaChange}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                // 한글 IME 조합 중 엔터 입력 시 중복 전송 방지
                if (e.nativeEvent.isComposing) return;
                e.preventDefault();
                handleSend();
              }
            }}
            placeholder="문서 내용에 대해 질문하세요... (마크다운 지원)"
            disabled={isPending}
            className="flex-1 min-h-[42px] max-h-[160px] resize-none border border-kds-gray-300 rounded-lg px-3.5 py-2.5 text-xs sm:text-sm text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 disabled:bg-kds-gray-50 transition-colors leading-relaxed"
          />
          <button
            type="submit"
            disabled={!inputContent.trim() || isPending}
            className="h-[42px] px-5 bg-kds-blue-600 hover:bg-kds-blue-800 text-white rounded-lg text-xs sm:text-sm font-semibold inline-flex items-center justify-center transition-colors disabled:opacity-40 flex-shrink-0 shadow-xs"
          >
            <span>전송</span>
          </button>
        </form>
      </div>
    </div>
  );
}
