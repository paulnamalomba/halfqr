import type { CredentialStatus, PaymentStatus } from "@/lib/types/account";

const credentialBadges: Record<CredentialStatus, { className: string; label: string }> = {
  active: { className: "badge badge-success", label: "Active" },
  expired: { className: "badge badge-warning", label: "Expired" },
  revoked: { className: "badge badge-muted", label: "Revoked" },
};

const paymentBadges: Record<PaymentStatus, { className: string; label: string }> = {
  Pending: { className: "badge badge-info", label: "Awaiting approval" },
  Completed: { className: "badge badge-success", label: "Paid" },
  Failed: { className: "badge badge-danger", label: "Declined" },
  Cancelled: { className: "badge badge-muted", label: "Cancelled" },
  Expired: { className: "badge badge-muted", label: "Expired" },
  NeedsReview: { className: "badge badge-warning", label: "Under review" },
};

export function CredentialStatusBadge({ status }: { status: CredentialStatus }) {
  const badge = credentialBadges[status];
  return <span className={badge.className}>{badge.label}</span>;
}

export function PaymentStatusBadge({ status }: { status: PaymentStatus }) {
  const badge = paymentBadges[status];
  return <span className={badge.className}>{badge.label}</span>;
}
