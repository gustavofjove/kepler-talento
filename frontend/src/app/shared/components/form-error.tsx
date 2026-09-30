import './form-error.css';

interface FormErrorProps {
  /** The message to show; nothing renders while it is empty. */
  message: string | null | undefined;
  testId?: string;
  /** Extra classes, e.g. `span-all` inside a grid form. */
  className?: string;
}

/**
 * A form's validation or save error (KTL-34): red, announced as an alert, and placed first in the
 * form so it is seen before the fields rather than after them.
 */
export function FormError({ message, testId, className }: FormErrorProps) {
  if (!message) return null;
  return (
    <p
      className={className ? `form-error ${className}` : 'form-error'}
      role="alert"
      data-testid={testId}
    >
      {message}
    </p>
  );
}
