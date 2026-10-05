"use client";

import {
  useEffect,
  useId,
  useRef,
  useState,
  cloneElement,
  isValidElement,
  type ButtonHTMLAttributes,
  type DialogHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from "react";
import { Icon } from "./icon";

export type ButtonVariant = "primary" | "secondary" | "quiet" | "danger";

const buttonVariants: Record<ButtonVariant, string> = {
  primary: "button-primary",
  secondary: "button-secondary",
  quiet: "button-quiet",
  danger: "button-danger",
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  busy?: boolean;
}

export function Button({
  children,
  className = "",
  variant = "secondary",
  busy = false,
  disabled,
  ...props
}: ButtonProps) {
  return (
    <button
      className={`button ${buttonVariants[variant]} ${className}`.trim()}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      {...props}
    >
      {busy ? <span className="button-progress" aria-hidden="true" /> : null}
      {children}
    </button>
  );
}

export function IconButton({
  label,
  icon,
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  label: string;
  icon: "menu" | "close" | "chevron" | "plus" | "logout";
}) {
  return (
    <button
      className={`icon-button ${className}`.trim()}
      aria-label={label}
      title={label}
      {...props}
    >
      <Icon name={icon} className="size-4" />
    </button>
  );
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`field-input ${props.className ?? ""}`.trim()} />;
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea {...props} className={`field-input field-textarea ${props.className ?? ""}`.trim()} />
  );
}

export function Field({
  label,
  hint,
  error,
  htmlFor,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  htmlFor: string;
  children: ReactNode;
}) {
  const hintId = useId();
  const errorId = useId();
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ");
  const control = isValidElement<Record<string, unknown>>(children)
    ? cloneElement(children, {
        "aria-describedby": describedBy || undefined,
        "aria-invalid": error ? true : undefined,
      })
    : children;

  return (
    <div className="field">
      <label className="field-label" htmlFor={htmlFor}>
        {label}
      </label>
      {control}
      {hint ? (
        <p className="field-hint" id={hintId}>
          {hint}
        </p>
      ) : null}
      {error ? (
        <p className="field-error" id={errorId} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}

export type BadgeTone = "neutral" | "accent" | "positive" | "warning" | "negative";

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: BadgeTone;
}) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

export function Separator({ className = "" }: { className?: string }) {
  return <div className={`separator ${className}`.trim()} role="separator" />;
}

export function Skeleton({
  className = "",
}: {
  className?: string;
}) {
  return <span className={`skeleton ${className}`.trim()} aria-hidden="true" />;
}

export function EmptyState({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow?: string;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  const titleId = useId();
  return (
    <section className="empty-state" aria-labelledby={titleId}>
      {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
      <h2 id={titleId}>{title}</h2>
      <p>{description}</p>
      {action ? <div className="empty-state-action">{action}</div> : null}
    </section>
  );
}

export function ErrorState({
  title = "This area could not be loaded",
  message,
  onRetry,
}: {
  title?: string;
  message: string;
  onRetry?: () => void;
}) {
  return (
    <section className="error-state" role="alert">
      <p className="eyebrow">Unable to continue</p>
      <h2>{title}</h2>
      <p>{message}</p>
      {onRetry ? (
        <Button type="button" onClick={onRetry}>
          Try again
        </Button>
      ) : null}
    </section>
  );
}

export function Table({
  children,
  label,
}: {
  children: ReactNode;
  label: string;
}) {
  return (
    <div className="table-scroll">
      <table aria-label={label}>{children}</table>
    </div>
  );
}

export interface TabItem {
  id: string;
  label: string;
  content: ReactNode;
}

export function Tabs({
  items,
  label,
  initialTab,
}: {
  items: TabItem[];
  label: string;
  initialTab?: string;
}) {
  const [activeTab, setActiveTab] = useState(initialTab ?? items[0]?.id ?? "");
  const tabListId = useId();

  if (items.length === 0) {
    return null;
  }

  const selected = items.find((item) => item.id === activeTab) ?? items[0];

  function moveFocus(currentId: string, offset: number) {
    const index = items.findIndex((item) => item.id === currentId);
    const nextIndex = (index + offset + items.length) % items.length;
    setActiveTab(items[nextIndex].id);
    document.getElementById(`${tabListId}-${items[nextIndex].id}`)?.focus();
  }

  function moveToBoundary(index: number) {
    setActiveTab(items[index].id);
    document.getElementById(`${tabListId}-${items[index].id}`)?.focus();
  }

  return (
    <div className="tabs">
      <div className="tab-list" role="tablist" aria-label={label}>
        {items.map((item) => (
          <button
            key={item.id}
            id={`${tabListId}-${item.id}`}
            type="button"
            role="tab"
            aria-selected={selected.id === item.id}
            aria-controls={`${tabListId}-panel`}
            tabIndex={selected.id === item.id ? 0 : -1}
            className="tab-trigger"
            onClick={() => setActiveTab(item.id)}
            onKeyDown={(event) => {
              if (event.key === "ArrowRight") {
                event.preventDefault();
                moveFocus(item.id, 1);
              } else if (event.key === "ArrowLeft") {
                event.preventDefault();
                moveFocus(item.id, -1);
              } else if (event.key === "Home") {
                event.preventDefault();
                moveToBoundary(0);
              } else if (event.key === "End") {
                event.preventDefault();
                moveToBoundary(items.length - 1);
              }
            }}
          >
            {item.label}
          </button>
        ))}
      </div>
      <div
        id={`${tabListId}-panel`}
        className="tab-panel"
        role="tabpanel"
        aria-labelledby={`${tabListId}-${selected.id}`}
        tabIndex={0}
      >
        {selected.content}
      </div>
    </div>
  );
}

export function Tooltip({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <span className="tooltip" tabIndex={0} aria-label={label} title={label}>
      {children}
    </span>
  );
}

export interface DialogProps
  extends Omit<DialogHTMLAttributes<HTMLDialogElement>, "open" | "onClose"> {
  open: boolean;
  title: string;
  onClose: () => void;
  placement?: "center" | "right" | "left";
  children: ReactNode;
}

export function Dialog({
  open,
  title,
  onClose,
  placement = "center",
  children,
  className = "",
  ...props
}: DialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      {...props}
      ref={dialogRef}
      aria-labelledby={titleId}
      className={`dialog dialog-${placement} ${className}`.trim()}
      onCancel={(event) => {
        props.onCancel?.(event);
        if (!event.defaultPrevented) onClose();
      }}
      onClose={() => onClose()}
    >
      <div className="dialog-surface">
        <header className="dialog-header">
          <h2 id={titleId}>{title}</h2>
          <IconButton type="button" label="Close dialog" icon="close" onClick={onClose} />
        </header>
        <div className="dialog-body">{children}</div>
      </div>
    </dialog>
  );
}

export function Drawer(props: Omit<DialogProps, "placement">) {
  return <Dialog {...props} placement="right" />;
}
