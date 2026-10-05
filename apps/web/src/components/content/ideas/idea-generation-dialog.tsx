"use client";

import { useId, useState, type FormEvent } from "react";
import { Button, Dialog, Field, Input } from "@/components/ui/primitives";
import type { ContentPlatform, GenerateContentIdeasRequest } from "@/lib/api/models";

interface IdeaGenerationDialogProps {
  open: boolean;
  onClose: () => void;
  onGenerate: (request: GenerateContentIdeasRequest) => Promise<void>;
  busy: boolean;
  error: string | null;
  contextReady: boolean;
}

export function IdeaGenerationDialog({
  open,
  onClose,
  onGenerate,
  busy,
  error,
  contextReady,
}: IdeaGenerationDialogProps) {
  const platformId = useId();
  const countId = useId();
  const topicId = useId();
  const [platform, setPlatform] = useState<ContentPlatform>("LinkedIn");
  const [count, setCount] = useState(5);
  const [topicHint, setTopicHint] = useState("");

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void onGenerate({
      platform,
      count,
      ...(topicHint.trim() ? { topicHint: topicHint.trim() } : {}),
    }).catch(() => undefined);
  }

  return (
    <Dialog
      open={open}
      title="Generate ideas"
      onClose={onClose}
      className="idea-generation-dialog"
    >
      <form className="idea-generation-form" onSubmit={submit}>
        <p className="idea-generation-intro">
          Build a considered set of directions from this workspace&apos;s saved brand,
          audience and content strategy. Choose a platform and an optional topic to focus
          the next pass.
        </p>
        <div className="idea-context-note">
          <span className="eyebrow">STRATEGY CONTEXT</span>
          <p>
            {contextReady
              ? "Your saved brand voice, audience and objectives are applied automatically."
              : "Add the workspace brand, audience and content strategy before requesting ideas."}
          </p>
        </div>
        <div className="idea-generation-fields">
          <Field label="Platform" htmlFor={platformId}>
            <select
              id={platformId}
              className="field-input"
              value={platform}
              onChange={(event) => setPlatform(event.target.value as ContentPlatform)}
            >
              <option value="LinkedIn">LinkedIn</option>
              <option value="X">X</option>
            </select>
          </Field>
          <Field label="Number of ideas" htmlFor={countId} hint="Choose between 1 and 20.">
            <Input
              id={countId}
              type="number"
              min={1}
              max={20}
              required
              value={count}
              onChange={(event) => setCount(Number(event.target.value))}
            />
          </Field>
          <Field
            label="Topic focus"
            htmlFor={topicId}
            hint="Optional. Keep it brief; up to 300 characters."
          >
            <Input
              id={topicId}
              maxLength={300}
              value={topicHint}
              onChange={(event) => setTopicHint(event.target.value)}
              placeholder="e.g. practical lessons from a recent project"
            />
          </Field>
        </div>
        {!contextReady ? (
          <p className="idea-context-blocked" role="status">
            Complete the workspace&apos;s brand, audience and content strategy before
            generating ideas.
          </p>
        ) : null}
        {error ? (
          <p className="auth-error" role="alert">
            {error}
          </p>
        ) : null}
        <div className="dialog-actions">
          <Button type="button" variant="quiet" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            busy={busy}
            disabled={!contextReady || count < 1 || count > 20}
          >
            Generate ideas
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
