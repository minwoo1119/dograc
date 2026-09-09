"use client";

import { useState } from "react";
import Image from "next/image";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { Plus, ChevronDown, Trash2 } from "lucide-react";
import { AuthModal } from "@/components/AuthModal";

export function Navbar() {
  const queryClient = useQueryClient();
  const {
    userId,
    currentUser,
    logout,
    currentWorkspaceId,
    setCurrentWorkspaceId,
  } = useAppStore();

  const [isCreating, setIsCreating] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState("");
  const [showAuthModal, setShowAuthModal] = useState(false);

  const { data: workspaces = [] } = useQuery({
    queryKey: ["workspaces", userId],
    queryFn: () => api.getWorkspaces(userId),
  });

  const createMutation = useMutation({
    mutationFn: (name: string) => api.createWorkspace(userId, name),
    onSuccess: (newWs) => {
      queryClient.invalidateQueries({ queryKey: ["workspaces", userId] });
      setCurrentWorkspaceId(newWs.id);
      setIsCreating(false);
      setNewWorkspaceName("");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteWorkspace(userId, id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workspaces", userId] });
      setCurrentWorkspaceId(null);
    },
  });

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newWorkspaceName.trim()) return;
    createMutation.mutate(newWorkspaceName.trim());
  };

  const selectedWorkspace = workspaces.find((ws) => ws.id === currentWorkspaceId);

  return (
    <header className="h-16 bg-white border-b border-kds-gray-300 sticky top-0 z-30 select-none flex-shrink-0">
      <div className="max-w-[1440px] mx-auto h-full px-6 flex items-center justify-between">
        {/* 좌측 브랜드 로고 및 워크스페이스 선택기 */}
        <div className="flex items-center space-x-6">
          <div className="flex items-center space-x-3">
            <Image
              src="/logo.svg"
              alt="dograc 로고"
              width={34}
              height={34}
              className="w-[34px] h-[34px] rounded-md"
              priority
            />
            <div className="flex items-center space-x-2">
              <span className="text-lg font-bold tracking-tight text-kds-gray-900 font-sans">
                dograc
              </span>
              <span className="text-[11px] font-semibold text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-2 py-0.5 rounded">
                RAG
              </span>
            </div>
          </div>

          <div className="h-5 w-[1px] bg-kds-gray-300" />

          {/* 워크스페이스 선택 셀렉터 */}
          <div className="flex items-center space-x-2">
            <div className="relative">
              <select
                value={currentWorkspaceId || ""}
                onChange={(e) => setCurrentWorkspaceId(e.target.value || null)}
                className="appearance-none bg-kds-gray-50 border border-kds-gray-300 text-kds-gray-900 text-xs sm:text-sm font-medium rounded-lg pl-3.5 pr-9 h-10 hover:border-kds-gray-400 focus:border-kds-blue-700 focus:bg-white focus:outline-none transition-colors cursor-pointer min-w-[180px]"
              >
                <option value="">워크스페이스 선택</option>
                {workspaces.map((ws) => (
                  <option key={ws.id} value={ws.id}>
                    {ws.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="w-4 h-4 text-kds-gray-500 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" strokeWidth={1.75} />
            </div>

            <button
              onClick={() => setIsCreating(true)}
              className="inline-flex items-center space-x-1.5 h-10 px-3.5 text-xs sm:text-sm font-medium text-kds-gray-700 bg-kds-gray-50 hover:bg-kds-gray-100 border border-kds-gray-300 rounded-lg transition-colors"
              title="새 워크스페이스 추가"
            >
              <Plus className="w-4 h-4" strokeWidth={1.75} />
              <span>새로 만들기</span>
            </button>

            {currentWorkspaceId && (
              <button
                onClick={() => {
                  if (confirm(`'${selectedWorkspace?.name || "현재"}' 워크스페이스를 삭제하시겠습니까?`)) {
                    deleteMutation.mutate(currentWorkspaceId);
                  }
                }}
                className="h-10 w-10 flex items-center justify-center text-kds-gray-400 hover:text-kds-red-500 hover:bg-kds-red-50 rounded-lg transition-colors"
                title="워크스페이스 삭제"
              >
                <Trash2 className="w-4 h-4" strokeWidth={1.75} />
              </button>
            )}
          </div>
        </div>

        {/* 우측 사용자 세션 정보 */}
        <div className="flex items-center space-x-3">
          {currentUser ? (
            <>
              <div
                className="flex items-center space-x-2.5 bg-kds-gray-50 border border-kds-gray-300 rounded-lg px-3 h-10 text-xs sm:text-sm"
                title={`${currentUser.email} (${currentUser.id})`}
              >
                <div className="w-6 h-6 rounded-full bg-kds-blue-600 text-white flex items-center justify-center text-[11px] font-bold select-none">
                  {currentUser.username[0]?.toUpperCase() || "U"}
                </div>
                <span className="font-semibold text-kds-gray-900">
                  {currentUser.username}
                </span>
              </div>
              <button
                onClick={() => logout()}
                className="h-10 px-4 rounded-lg border border-kds-gray-300 bg-white hover:bg-kds-gray-100 text-kds-gray-700 text-xs sm:text-sm font-medium transition-colors"
                title="로그아웃"
              >
                로그아웃
              </button>
            </>
          ) : (
            <button
              onClick={() => setShowAuthModal(true)}
              className="h-10 px-5 rounded-lg bg-kds-blue-600 hover:bg-kds-blue-800 text-white text-xs sm:text-sm font-semibold transition-colors shadow-sm"
            >
              로그인
            </button>
          )}
        </div>
      </div>

      {/* 새 워크스페이스 모달 (KDS Standard Modal) */}
      {isCreating && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-[2px] flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded shadow-dropdown max-w-sm w-full p-6 space-y-4 border border-kds-gray-300 animate-in fade-in zoom-in-95 duration-150">
            <div>
              <h3 className="text-sm font-bold text-kds-gray-900">새 워크스페이스 만들기</h3>
              <p className="text-xs text-kds-gray-600 mt-1">
                주제별로 문서를 분리하여 보관하고 질의응답을 진행할 수 있습니다.
              </p>
            </div>

            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                  워크스페이스 이름
                </label>
                <input
                  type="text"
                  value={newWorkspaceName}
                  onChange={(e) => setNewWorkspaceName(e.target.value)}
                  placeholder="예: 2026 연구 논문 정리"
                  className="w-full border border-kds-gray-300 rounded px-3 py-2 text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
                  autoFocus
                  required
                />
              </div>

              <div className="flex justify-end space-x-2 pt-1">
                <button
                  type="button"
                  onClick={() => setIsCreating(false)}
                  className="px-3 py-1.5 text-xs font-medium text-kds-gray-700 hover:bg-kds-gray-100 rounded transition-colors"
                >
                  취소
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-3.5 py-1.5 text-xs font-semibold bg-kds-blue-600 hover:bg-kds-blue-800 text-white rounded transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? "생성 중..." : "생성하기"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 로그인 및 회원가입 모달 */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={() => setShowAuthModal(false)}
      />
    </header>
  );
}
