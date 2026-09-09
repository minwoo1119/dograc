"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { X, AlertCircle } from "lucide-react";

interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function AuthModal({ isOpen, onClose }: AuthModalProps) {
  const queryClient = useQueryClient();
  const { login } = useAppStore();

  const [mode, setMode] = useState<"login" | "register">("login");
  const [emailOrUsername, setEmailOrUsername] = useState("");
  const [email, setEmail] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  const loginMutation = useMutation({
    mutationFn: () => api.login(emailOrUsername.trim(), password),
    onSuccess: (data) => {
      login(data.user, data.access_token);
      queryClient.clear();
      onClose();
      resetForm();
    },
    onError: (err: Error) => {
      setError(err.message || "로그인에 실패했습니다.");
    },
  });

  const registerMutation = useMutation({
    mutationFn: () =>
      api.register(email.trim(), username.trim(), password),
    onSuccess: (data) => {
      login(data.user, data.access_token);
      queryClient.clear();
      onClose();
      resetForm();
    },
    onError: (err: Error) => {
      setError(err.message || "회원가입에 실패했습니다.");
    },
  });

  const resetForm = () => {
    setEmailOrUsername("");
    setEmail("");
    setUsername("");
    setPassword("");
    setError(null);
  };

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (mode === "login") {
      if (!emailOrUsername.trim() || !password) {
        setError("아이디와 비밀번호를 모두 입력해 주세요.");
        return;
      }
      loginMutation.mutate();
    } else {
      if (!email.trim() || !username.trim() || !password) {
        setError("모든 입력란을 채워 주세요.");
        return;
      }
      if (password.length < 4) {
        setError("비밀번호는 최소 4자 이상이어야 합니다.");
        return;
      }
      registerMutation.mutate();
    }
  };

  const handleFillDemo = () => {
    setMode("login");
    setEmailOrUsername("admin");
    setPassword("password123");
    setError(null);
  };

  const isLoading = loginMutation.isPending || registerMutation.isPending;

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4 backdrop-blur-[1px]">
      <div className="bg-white rounded border border-kds-gray-300 w-full max-w-sm shadow-modal p-6 relative">
        {/* 닫기 버튼 */}
        <button
          onClick={() => {
            resetForm();
            onClose();
          }}
          className="absolute top-4 right-4 p-1 text-kds-gray-400 hover:text-kds-gray-700 hover:bg-kds-gray-100 rounded transition-colors"
        >
          <X className="w-4 h-4 stroke-[1.5]" />
        </button>

        {/* 탭 전환 (로그인 / 회원가입) */}
        <div className="flex border-b border-kds-gray-200 mb-5">
          <button
            type="button"
            onClick={() => {
              setMode("login");
              setError(null);
            }}
            className={`flex-1 pb-2.5 text-xs font-semibold text-center transition-colors relative ${
              mode === "login"
                ? "text-kds-blue-600 border-b-2 border-kds-blue-600 -mb-[1px]"
                : "text-kds-gray-500 hover:text-kds-gray-900"
            }`}
          >
            로그인
          </button>
          <button
            type="button"
            onClick={() => {
              setMode("register");
              setError(null);
            }}
            className={`flex-1 pb-2.5 text-xs font-semibold text-center transition-colors relative ${
              mode === "register"
                ? "text-kds-blue-600 border-b-2 border-kds-blue-600 -mb-[1px]"
                : "text-kds-gray-500 hover:text-kds-gray-900"
            }`}
          >
            회원가입
          </button>
        </div>

        {/* 에러 메시지 */}
        {error && (
          <div className="mb-4 p-2.5 bg-kds-red-50 border border-kds-red-100 rounded text-kds-red-600 text-xs flex items-center space-x-1.5">
            <AlertCircle className="w-3.5 h-3.5 flex-shrink-0 stroke-[1.5]" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-3">
          {mode === "login" ? (
            <div>
              <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                아이디 또는 이메일
              </label>
              <input
                type="text"
                value={emailOrUsername}
                onChange={(e) => setEmailOrUsername(e.target.value)}
                placeholder="admin 또는 이메일 주소"
                className="w-full h-9 px-3 bg-white border border-kds-gray-300 rounded text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
              />
            </div>
          ) : (
            <>
              <div>
                <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                  이메일 주소
                </label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  className="w-full h-9 px-3 bg-white border border-kds-gray-300 rounded text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
                  사용자명
                </label>
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="영문, 숫자 2자 이상"
                  className="w-full h-9 px-3 bg-white border border-kds-gray-300 rounded text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
                />
              </div>
            </>
          )}

          <div>
            <label className="block text-[11px] font-medium text-kds-gray-700 mb-1">
              비밀번호
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="비밀번호 입력"
              className="w-full h-9 px-3 bg-white border border-kds-gray-300 rounded text-xs text-kds-gray-900 placeholder:text-kds-gray-400 focus:outline-none focus:border-kds-blue-600 focus:ring-1 focus:ring-kds-blue-600 transition-colors"
            />
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full h-9 bg-kds-blue-600 hover:bg-kds-blue-800 disabled:bg-kds-gray-300 text-white text-xs font-semibold rounded transition-colors flex items-center justify-center mt-2"
          >
            {isLoading ? (
              <span>처리 중...</span>
            ) : mode === "login" ? (
              <span>로그인하기</span>
            ) : (
              <span>회원가입 완료</span>
            )}
          </button>
        </form>

        {/* 데모 계정 자동 채우기 버튼 */}
        <div className="mt-4 pt-3.5 border-t border-kds-gray-200">
          <button
            type="button"
            onClick={handleFillDemo}
            className="w-full h-8 px-3 bg-kds-gray-50 hover:bg-kds-gray-100 border border-kds-gray-300 rounded text-[11px] text-kds-gray-700 font-medium transition-colors flex items-center justify-center"
          >
            <span>데모 관리자 계정 채우기 (admin)</span>
          </button>
        </div>
      </div>
    </div>
  );
}
