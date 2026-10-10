"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { Notice } from "@/components/console/ui";
import type { AuthConfig, ProblemDetails } from "@/lib/types/account";
import { GoogleMark } from "./google-mark";

type AuthIntent = "sign-in" | "sign-up";

const errorMessages: Record<string, string> = {
  google_not_configured: "Google sign-in is not available yet. Use an email link.",
  google_cancelled: "Google sign-in was cancelled.",
  google_state_mismatch: "That sign-in attempt expired. Try again.",
  google_failed: "Google could not verify your account. Try again.",
  email_domain_blocked: "Disposable and blocked email domains are not accepted.",
  email_link_invalid: "That link is invalid, used or expired. Request a new one.",
};

const copy: Record<AuthIntent, { title: string; description: string; google: string; switchText: string; switchLink: string; switchHref: string }> = {
  "sign-in": {
    title: "Sign in",
    description: "Manage API keys, tokens and billing.",
    google: "Continue with Google",
    switchText: "New to HalfQR?",
    switchLink: "Create an account",
    switchHref: "/sign-up",
  },
  "sign-up": {
    title: "Create an account",
    description: "Free to start. Upgrade when you need API keys.",
    google: "Sign up with Google",
    switchText: "Have an account?",
    switchLink: "Sign in",
    switchHref: "/sign-in",
  },
};

// Reads the first validation message, falling back to the problem title.
function problemMessage(problem: ProblemDetails | null, field: string, fallback: string) {
  return problem?.errors?.[field]?.[0] ?? problem?.title ?? fallback;
}

async function postJson(url: string, body: unknown) {
  const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  return { ok: response.ok, problem: response.ok ? null : ((await response.json().catch(() => null)) as ProblemDetails | null) };
}

export function AuthPanel({ intent, config, error, next, signedOut }: { intent: AuthIntent; config: AuthConfig; error?: string; next?: string; signedOut?: boolean }) {
  const router = useRouter();
  const text = copy[intent];
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState<string | null>(null);
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [devEmail, setDevEmail] = useState("developer@halfqr.dev");
  const [devError, setDevError] = useState<string | null>(null);

  const googleHref = `/api/auth/google/start?intent=${intent}${next ? `&next=${encodeURIComponent(next)}` : ""}`;
  const pageError = error ? errorMessages[error] ?? "Sign-in failed. Try again." : null;

  async function sendEmailLink(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setEmailError(null);

    try {
      const result = await postJson("/api/auth/email/start", { email });

      if (result.ok) {
        setSentTo(email.trim().toLowerCase());
      } else {
        setEmailError(problemMessage(result.problem, "email", "We could not send the link."));
      }
    } catch {
      setEmailError("We could not reach HalfQR.");
    } finally {
      setSubmitting(false);
    }
  }

  async function devSignIn(event: FormEvent) {
    event.preventDefault();
    setDevError(null);
    const result = await postJson("/api/auth/dev", { email: devEmail });

    if (result.ok) {
      router.push(next ?? "/dashboard");
      router.refresh();
    } else {
      setDevError(problemMessage(result.problem, "email", "Development sign-in failed."));
    }
  }

  return (
    <main className="auth-main">
      <div className="auth-card">
        <div className="auth-card-head">
          <h1>{text.title}</h1>
          <p>{text.description}</p>
        </div>

        {pageError ? (
          <Notice tone="danger" role="alert">
            {pageError}
          </Notice>
        ) : null}
        {signedOut ? (
          <Notice tone="info" role="status">
            You have been signed out.
          </Notice>
        ) : null}

        {sentTo ? (
          <Notice tone="success" title="Check your inbox" role="status">
            We sent a one-time link to {sentTo}. It expires in 15 minutes.{" "}
            <button type="button" className="btn btn-ghost" onClick={() => setSentTo(null)}>
              Use another email
            </button>
          </Notice>
        ) : (
          <>
            <a className="btn btn-google btn-block" href={googleHref}>
              <GoogleMark />
              {text.google}
            </a>

            <div className="auth-divider">or</div>

            <form className="form-stack" onSubmit={sendEmailLink} noValidate>
              <div className="console-field">
                <label className="console-label" htmlFor="auth-email">
                  Email
                </label>
                <input
                  id="auth-email"
                  className="console-input"
                  type="email"
                  autoComplete="email"
                  placeholder="you@company.com"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  aria-invalid={emailError ? true : undefined}
                  aria-describedby={emailError ? "auth-email-error" : undefined}
                  required
                />
                {emailError ? (
                  <span id="auth-email-error" className="console-field-error">
                    {emailError}
                  </span>
                ) : null}
              </div>
              <button type="submit" className="btn btn-primary btn-block" disabled={submitting || !config.emailEnabled || email.trim().length === 0}>
                {submitting ? "Sending…" : "Email me a link"}
              </button>
            </form>
          </>
        )}

        {config.devSignInEnabled ? (
          <details className="auth-dev">
            <summary>Local development sign-in</summary>
            <form onSubmit={devSignIn}>
              <input className="console-input" type="email" value={devEmail} onChange={(event) => setDevEmail(event.target.value)} aria-label="Development email" />
              {devError ? <span className="console-field-error">{devError}</span> : null}
              <button type="submit" className="btn btn-secondary">
                Sign in without email
              </button>
            </form>
          </details>
        ) : null}

        <p className="auth-foot">
          {text.switchText} <Link href={text.switchHref}>{text.switchLink}</Link> · <Link href="/">QR builder</Link>
        </p>
      </div>
    </main>
  );
}
