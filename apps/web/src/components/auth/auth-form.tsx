"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { useSession } from "@/components/session/session-provider";
import { Icon } from "@/components/ui/icon";
import { Button, Field, Input } from "@/components/ui/primitives";

type AuthMode = "login" | "register";

export function AuthForm({ mode }: { mode: AuthMode }) {
  const router = useRouter();
  const { signIn, register } = useSession();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isRegister = mode === "register";

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    const formData = new FormData(event.currentTarget);
    const email = String(formData.get("email") ?? "").trim();
    const password = String(formData.get("password") ?? "");

    try {
      if (isRegister) {
        await register({
          email,
          password,
          firstName: String(formData.get("firstName") ?? "").trim(),
          lastName: String(formData.get("lastName") ?? "").trim(),
          workspaceName: String(formData.get("workspaceName") ?? "").trim(),
        });
      } else {
        await signIn({ email, password });
      }

      router.replace("/");
      router.refresh();
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Your request could not be completed. Try again.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="auth-frame">
      <aside className="auth-aside">
        <Link className="auth-brand" href="/" aria-label="Mayorx Content Engine">
          <span className="brand-mark" aria-hidden="true">MX</span>
          <span>
            Mayorx
            <span className="brand-caption">Content Engine</span>
          </span>
        </Link>
        <div className="auth-editorial">
          <p className="eyebrow">A working space for ideas</p>
          <h1>Make every piece of content part of a bigger story.</h1>
          <p>
            Strategy, writing, publishing, and learning—connected in one considered
            workflow.
          </p>
        </div>
        <p className="auth-footer">Strategy / Creation / Distribution / Learning</p>
      </aside>

      <section className="auth-form-region" aria-labelledby="auth-heading">
        <form className="auth-form" onSubmit={submit}>
          <div>
            <p className="eyebrow">{isRegister ? "Create an account" : "Your workspace"}</p>
            <h2 id="auth-heading">{isRegister ? "Begin with the essentials." : "Welcome back."}</h2>
          </div>
          <p className="auth-form-intro">
            {isRegister
              ? "Your account and first workspace are created together."
              : "Sign in to return to your active workspace."}
          </p>

          <div className="auth-form-fields">
            {isRegister ? (
              <>
                <Field label="First name" htmlFor="firstName">
                  <Input
                    id="firstName"
                    name="firstName"
                    autoComplete="given-name"
                    required
                  />
                </Field>
                <Field label="Last name" htmlFor="lastName">
                  <Input
                    id="lastName"
                    name="lastName"
                    autoComplete="family-name"
                    required
                  />
                </Field>
                <Field
                  label="Workspace name"
                  htmlFor="workspaceName"
                  hint="This is the first content environment attached to your account."
                >
                  <Input
                    id="workspaceName"
                    name="workspaceName"
                    autoComplete="organization"
                    required
                  />
                </Field>
              </>
            ) : null}
            <Field label="Email" htmlFor="email">
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                autoCapitalize="none"
                required
              />
            </Field>
            <Field label="Password" htmlFor="password">
              <Input
                id="password"
                name="password"
                type="password"
                autoComplete={isRegister ? "new-password" : "current-password"}
                required
              />
            </Field>
          </div>

          {error ? (
            <p className="auth-error" role="alert">
              {error}
            </p>
          ) : null}

          <Button className="auth-submit" type="submit" variant="primary" busy={busy}>
            {isRegister ? "Create account" : "Sign in"}
          </Button>

          <p className="auth-switch">
            {isRegister ? "Already have an account? " : "New to Mayorx? "}
            <Link href={isRegister ? "/login" : "/register"}>
              {isRegister ? "Sign in" : "Create an account"}
            </Link>
          </p>
          <span className="auth-security-note">
            <Icon name="connections" className="size-4" />
            Session credentials stay in secure, httpOnly cookies.
          </span>
        </form>
      </section>
    </main>
  );
}
