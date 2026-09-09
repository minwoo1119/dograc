"use client";

import React, { ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

interface MarkdownRendererProps {
  content: string;
  className?: string;
  isUser?: boolean;
}

// [파일명, p.숫자] 패턴을 감지하여 KDS 인라인 뱃지로 변환
function parseCitations(text: string): ReactNode[] {
  const citationRegex = /\[([^\]]+),\s*(p\.\d+)\]/g;
  const parts: ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = citationRegex.exec(text)) !== null) {
    const start = match.index;
    const end = citationRegex.lastIndex;

    // 이전 일반 텍스트
    if (start > lastIndex) {
      parts.push(text.substring(lastIndex, start));
    }

    const fileName = match[1];
    const page = match[2];

    // 인라인 인용 뱃지
    parts.push(
      <span
        key={`citation-${start}`}
        className="inline-flex items-center space-x-1 px-1.5 py-0.2 mx-0.5 rounded text-[10.5px] font-semibold bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200 select-none align-baseline shadow-xs"
        title={`${fileName} (${page})`}
      >
        <span className="truncate max-w-[140px]">{fileName}</span>
        <span className="text-kds-blue-900 font-mono text-[10px]">{page}</span>
      </span>
    );

    lastIndex = end;
  }

  if (lastIndex < text.length) {
    parts.push(text.substring(lastIndex));
  }

  return parts.length > 0 ? parts : [text];
}

// React 자식 요소들 중 문자열을 찾아 인라인 인용 변환
function processChildren(children: ReactNode): ReactNode {
  return React.Children.map(children, (child) => {
    if (typeof child === "string") {
      return parseCitations(child);
    }
    return child;
  });
}

export function MarkdownRenderer({ content, className = "", isUser = false }: MarkdownRendererProps) {
  return (
    <div className={`markdown-content text-xs sm:text-sm leading-relaxed ${className}`}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          p({ children }) {
            return (
              <p className="mb-2 last:mb-0 leading-relaxed text-kds-gray-800">
                {isUser ? children : processChildren(children)}
              </p>
            );
          },
          li({ children }) {
            return (
              <li className="leading-relaxed text-kds-gray-800">
                {isUser ? children : processChildren(children)}
              </li>
            );
          },
          ul({ children }) {
            return (
              <ul className="list-disc list-inside space-y-1 my-2 text-kds-gray-800 pl-1">
                {children}
              </ul>
            );
          },
          ol({ children }) {
            return (
              <ol className="list-decimal list-inside space-y-1 my-2 text-kds-gray-800 pl-1">
                {children}
              </ol>
            );
          },
          h1({ children }) {
            return <h1 className="text-base font-bold text-kds-gray-900 mt-3 mb-1.5 border-b border-kds-gray-200 pb-1">{children}</h1>;
          },
          h2({ children }) {
            return <h2 className="text-sm font-bold text-kds-gray-900 mt-2.5 mb-1">{children}</h2>;
          },
          h3({ children }) {
            return <h3 className="text-xs sm:text-sm font-bold text-kds-gray-900 mt-2 mb-1">{children}</h3>;
          },
          blockquote({ children }) {
            return (
              <blockquote className="border-l-2 border-kds-blue-600 pl-3 py-1 my-2 bg-kds-gray-100/60 rounded-r text-kds-gray-700 italic text-xs">
                {children}
              </blockquote>
            );
          },
          code({ className, children, ...props }) {
            const isInline = !className && typeof children === "string" && !children.includes("\n");
            if (isInline) {
              return (
                <code
                  className="bg-kds-gray-100 text-kds-blue-700 px-1.5 py-0.5 rounded font-mono text-[11px] border border-kds-gray-200"
                  {...props}
                >
                  {children}
                </code>
              );
            }
            return (
              <pre className="my-2 p-3 bg-kds-gray-900 text-kds-gray-100 rounded-md font-mono text-xs overflow-x-auto leading-normal border border-kds-gray-800">
                <code className={className} {...props}>
                  {children}
                </code>
              </pre>
            );
          },
          table({ children }) {
            return (
              <div className="overflow-x-auto my-2 rounded border border-kds-gray-300">
                <table className="w-full text-left text-xs border-collapse">
                  {children}
                </table>
              </div>
            );
          },
          thead({ children }) {
            return <thead className="bg-kds-gray-100 border-b border-kds-gray-300 text-kds-gray-800 font-semibold">{children}</thead>;
          },
          tbody({ children }) {
            return <tbody className="divide-y divide-kds-gray-200">{children}</tbody>;
          },
          tr({ children }) {
            return <tr className="hover:bg-kds-gray-50/80 transition-colors">{children}</tr>;
          },
          th({ children }) {
            return <th className="px-3 py-1.5 text-kds-gray-800 font-semibold">{children}</th>;
          },
          td({ children }) {
            return <td className="px-3 py-1.5 text-kds-gray-700">{isUser ? children : processChildren(children)}</td>;
          },
          strong({ children }) {
            return <strong className="font-semibold text-kds-gray-900">{children}</strong>;
          },
          a({ href, children }) {
            return (
              <a
                href={href}
                target="_blank"
                rel="noopener noreferrer"
                className="text-kds-blue-700 hover:text-kds-blue-800 underline underline-offset-2"
              >
                {children}
              </a>
            );
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
