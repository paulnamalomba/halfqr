"use client";

import { useCallback, useEffect, useState } from "react";

export function useToast() {
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!message) {
      return;
    }

    const timeoutId = window.setTimeout(() => setMessage(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [message]);

  const toast = message ? (
    <div className="toast" role="status">
      {message}
    </div>
  ) : null;

  return { toast, showToast: useCallback((text: string) => setMessage(text), []) };
}
