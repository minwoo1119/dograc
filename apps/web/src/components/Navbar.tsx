"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { FolderPlus, Layers, Trash2, User } from "lucide-react";

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

  const { data: workspaces = [], isLoading } = useQuery({
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

  return (
    <header className="border-b border-slate-200 bg-white sticky top-0 z-30">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div className="flex items-center space-x-6">
          <div className="flex items-center space-x-2">
            <span className="text-2xl font-black tracking-tight text-indigo-600">dograc</span>
            <span className="text-xs px-2 py-0.5 font-semibold bg-indigo-50 text-indigo-700 rounded-full border border-indigo-200">
              RAG
            </span>
          </div>

          <div className="flex items-center space-x-2">
            <Layers className="w-4 h-4 text-slate-500" />
            <select
              value={currentWorkspaceId || ""}
              onChange={(e) => setCurrentWorkspaceId(e.target.value || null)}
              className="bg-slate-50 border border-slate-300 text-slate-800 text-sm rounded-lg px-3 py-1.5 focus:ring-2 focus:ring-indigo-500 focus:outline-none"
            >
              <option value="">-- 워크스페이스 선택 --</option>
              {workspaces.map((ws) => (
                <option key={ws.id} value={ws.id}>
                  {ws.name}
                </option>
              ))}
            </select>

            <button
              onClick={() => setIsCreating(true)}
              className="p-1.5 text-slate-600 hover:text-indigo-600 hover:bg-slate-100 rounded-lg transition"
              title="새 워크스페이스"
            >
              <FolderPlus className="w-4 h-4" />
            </button>

            {currentWorkspaceId && (
              <button
                onClick={() => {
                  if (confirm("이 워크스페이스와 연관 문서를 모두 삭제하시겠습니까?")) {
                    deleteMutation.mutate(currentWorkspaceId);
                  }
                }}
                className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                title="워크스페이스 삭제"
              >
                <Trash2 className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>

        <div className="flex items-center space-x-3">
          <button
            onClick={() => setShowUserModal(true)}
            className="flex items-center space-x-1.5 text-xs text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 px-2.5 py-1.5 rounded-md transition"
          >
            <User className="w-3.5 h-3.5" />
            <span className="font-mono">{userId.slice(0, 8)}...</span>
          </button>
        </div>
      </div>

      {/* 새 워크스페이스 모달 */}
      {isCreating && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-sm w-full p-5 space-y-4">
            <h3 className="text-base font-bold text-slate-900">새 워크스페이스 생성</h3>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-700 mb-1">
                  워크스페이스 이름
                </label>
                <input
                  type="text"
                  value={newWorkspaceName}
                  onChange={(e) => setNewWorkspaceName(e.target.value)}
                  placeholder="예: 사내 기술 규정집"
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  autoFocus
                  required
                />
              </div>
              <div className="flex justify-end space-x-2">
                <button
                  type="button"
                  onClick={() => setIsCreating(false)}
                  className="px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded-lg"
                >
                  취소
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-3 py-1.5 text-xs bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg disabled:opacity-50"
                >
                  {createMutation.isPending ? "생성 중..." : "생성"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 사용자 ID 설정 모달 */}
      {showUserModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-sm w-full p-5 space-y-4">
            <h3 className="text-base font-bold text-slate-900">개발자 사용자 ID (X-User-ID)</h3>
            <p className="text-xs text-slate-500">
              현재 인증이 없는 단계로, 워크스페이스 및 문서 데이터 격리를 위해 UUID 헤더를 사용합니다.
            </p>
            <input
              type="text"
              value={tempUserId}
              onChange={(e) => setTempUserId(e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-3 py-2 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <div className="flex justify-end space-x-2">
              <button
                type="button"
                onClick={() => setShowUserModal(false)}
                className="px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded-lg"
              >
                취소
              </button>
              <button
                type="button"
                onClick={() => {
                  setUserId(tempUserId.trim());
                  setShowUserModal(false);
                }}
                className="px-3 py-1.5 text-xs bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg"
              >
                저장
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
}
