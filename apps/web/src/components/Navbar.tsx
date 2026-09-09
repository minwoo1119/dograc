"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { Plus, ChevronDown, Trash2, KeyRound, Check } from "lucide-react";

export function Navbar() {
  const queryClient = useQueryClient();
  const {
    userId,
    setUserId,
    currentWorkspaceId,
    setCurrentWorkspaceId,
  } = useAppStore();

  const [isCreating, setIsCreating] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState("");
  const [showUserModal, setShowUserModal] = useState(false);
  const [tempUserId, setTempUserId] = useState(userId);

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
    <header className="h-14 bg-white border-b border-kds-gray-300 sticky top-0 z-30 select-none">
      <div className="max-w-[1440px] mx-auto h-full px-6 flex items-center justify-between">
        {/* 좌측 브랜드 로고 및 워크스페이스 선택기 */}
        <div className="flex items-center space-x-6">
          <div className="flex items-center space-x-2">
            <span className="text-lg font-bold tracking-tight text-kds-gray-900">
              dograc
            </span>
            <span className="text-[11px] font-medium text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-2 py-0.5 rounded-full">
              RAG
            </span>
          </div>

          <div className="h-4 w-[1px] bg-kds-gray-300" />

          {/* 워크스페이스 선택 셀렉터 */}
          <div className="flex items-center space-x-2">
            <div className="relative">
              <select
                value={currentWorkspaceId || ""}
                onChange={(e) => setCurrentWorkspaceId(e.target.value || null)}
                className="appearance-none bg-kds-gray-50 border border-kds-gray-300 text-kds-gray-900 text-xs font-medium rounded-lg pl-3 pr-8 py-2 hover:border-kds-gray-400 focus:border-kds-blue-700 focus:bg-white focus:outline-none transition-colors cursor-pointer min-w-[160px]"
              >
                <option value="">워크스페이스 선택</option>
                {workspaces.map((ws) => (
                  <option key={ws.id} value={ws.id}>
                    {ws.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="w-3.5 h-3.5 text-kds-gray-500 absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none" strokeWidth={1.75} />
            </div>

            <button
              onClick={() => setIsCreating(true)}
              className="inline-flex items-center space-x-1 h-8 px-2.5 text-xs font-medium text-kds-gray-700 bg-kds-gray-50 hover:bg-kds-gray-100 border border-kds-gray-300 rounded-lg transition-colors"
              title="새 워크스페이스 추가"
            >
              <Plus className="w-3.5 h-3.5" strokeWidth={1.75} />
              <span>새로 만들기</span>
            </button>

            {currentWorkspaceId && (
              <button
                onClick={() => {
                  if (confirm(`'${selectedWorkspace?.name || "현재"}' 워크스페이스를 삭제하시겠습니까?`)) {
                    deleteMutation.mutate(currentWorkspaceId);
                  }
                }}
                className="h-8 w-8 flex items-center justify-center text-kds-gray-400 hover:text-kds-red-500 hover:bg-kds-red-50 rounded-lg transition-colors"
                title="워크스페이스 삭제"
              >
                <Trash2 className="w-3.5 h-3.5" strokeWidth={1.75} />
              </button>
            )}
          </div>
        </div>

        {/* 우측 사용자 세션 정보 */}
        <div className="flex items-center space-x-3">
          <button
            onClick={() => setShowUserModal(true)}
            className="flex items-center space-x-1.5 h-8 px-3 rounded-lg border border-kds-gray-300 bg-white hover:bg-kds-gray-50 text-kds-gray-700 text-xs font-mono transition-colors"
            title="사용자 식별자 변경"
          >
            <KeyRound className="w-3 h-3 text-kds-gray-500" strokeWidth={1.75} />
            <span>{userId.slice(0, 8)}</span>
          </button>
        </div>
      </div>

      {/* 새 워크스페이스 모달 (KDS Standard Modal) */}
      {isCreating && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-[2px] flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-dropdown max-w-sm w-full p-6 space-y-5 border border-kds-gray-300 animate-in fade-in zoom-in-95 duration-150">
            <div>
              <h3 className="text-base font-bold text-kds-gray-900">새 워크스페이스 만들기</h3>
              <p className="text-xs text-kds-gray-600 mt-1">
                주제별로 문서를 분리하여 보관하고 질의응답을 진행할 수 있습니다.
              </p>
            </div>

            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-kds-gray-700 mb-1.5">
                  워크스페이스 이름
                </label>
                <input
                  type="text"
                  value={newWorkspaceName}
                  onChange={(e) => setNewWorkspaceName(e.target.value)}
                  placeholder="예: 2026 연구 논문 정리"
                  className="w-full border border-kds-gray-300 rounded-lg px-3.5 py-2.5 text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-700 transition-colors"
                  autoFocus
                  required
                />
              </div>

              <div className="flex justify-end space-x-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsCreating(false)}
                  className="px-4 py-2 text-xs font-medium text-kds-gray-700 hover:bg-kds-gray-100 rounded-lg transition-colors"
                >
                  취소
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-4 py-2 text-xs font-medium bg-kds-blue-700 hover:bg-kds-blue-800 text-white rounded-lg transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? "생성 중..." : "확인"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 사용자 ID 설정 모달 */}
      {showUserModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-[2px] flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-dropdown max-w-sm w-full p-6 space-y-4 border border-kds-gray-300 animate-in fade-in zoom-in-95 duration-150">
            <div>
              <h3 className="text-base font-bold text-kds-gray-900">사용자 식별자(X-User-ID) 설정</h3>
              <p className="text-xs text-kds-gray-600 mt-1 leading-relaxed">
                현재 개발 단계의 데이터 소유권 격리를 위해 클라이언트 UUID 헤더를 사용합니다.
              </p>
            </div>

            <div>
              <input
                type="text"
                value={tempUserId}
                onChange={(e) => setTempUserId(e.target.value)}
                className="w-full border border-kds-gray-300 rounded-lg px-3.5 py-2 text-xs font-mono text-kds-gray-900 focus:outline-none focus:border-kds-blue-700"
              />
            </div>

            <div className="flex justify-end space-x-2 pt-2">
              <button
                type="button"
                onClick={() => setShowUserModal(false)}
                className="px-4 py-2 text-xs font-medium text-kds-gray-700 hover:bg-kds-gray-100 rounded-lg transition-colors"
              >
                취소
              </button>
              <button
                type="button"
                onClick={() => {
                  setUserId(tempUserId.trim());
                  setShowUserModal(false);
                }}
                className="inline-flex items-center space-x-1 px-4 py-2 text-xs font-medium bg-kds-blue-700 hover:bg-kds-blue-800 text-white rounded-lg transition-colors"
              >
                <Check className="w-3.5 h-3.5" strokeWidth={2} />
                <span>적용하기</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
}
