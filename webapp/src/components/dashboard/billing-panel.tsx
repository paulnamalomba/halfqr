"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useState } from "react";
import { EmptyState, Notice, SectionCard } from "@/components/console/ui";
import { AccountApiError, accountRequest } from "@/lib/client/account-api";
import { formatDate, formatDateTime, formatMwk } from "@/lib/format";
import type { BillingOperator, BillingOverview, BillingPayment, BillingPlan, Entitlement } from "@/lib/types/account";
import { Dialog } from "./dialog";
import { PaymentStatusBadge } from "./status-badge";

const pollIntervalMs = 4000;

const freePlan: BillingPlan = {
  id: "free",
  name: "Free",
  description: "The public web builder.",
  amountMwk: 0,
  periodDays: 0,
  maxApiKeys: 0,
  maxAccessTokens: 0,
  requestsPerMinute: 30,
  highlighted: false,
  features: ["Unlimited QR codes in the builder", "SVG and PNG downloads", "Anonymous API, 30 requests per minute", "No API keys or tokens"],
};

function replacePayment(items: BillingPayment[], payment: BillingPayment) {
  return items.map((item) => (item.id === payment.id ? payment : item));
}

function planActionLabel(plan: BillingPlan, entitlement: Entitlement) {
  if (plan.id === entitlement.planId) {
    return "Renew 30 days";
  }

  return entitlement.isPaid ? `Switch to ${plan.name}` : `Choose ${plan.name}`;
}

function operatorLabel(operators: BillingOperator[], id: string) {
  return operators.find((operator) => operator.id === id)?.label ?? id;
}

export function BillingPanel({ overview }: { overview: BillingOverview }) {
  const router = useRouter();
  const [payments, setPayments] = useState(overview.payments);
  const [checkoutPlan, setCheckoutPlan] = useState<BillingPlan | null>(null);
  const [activePayment, setActivePayment] = useState<BillingPayment | null>(overview.payments.find((payment) => payment.status === "Pending") ?? null);
  const { entitlement } = overview;
  const isPending = activePayment?.status === "Pending";

  // Poll the pending charge until PayChangu reports a final status.
  useEffect(() => {
    if (!activePayment || activePayment.status !== "Pending") {
      return;
    }

    const timeoutId = window.setTimeout(async () => {
      try {
        const refreshed = await accountRequest<BillingPayment>(`billing/payments/${activePayment.id}/refresh`, { method: "POST" });
        setActivePayment(refreshed);
        setPayments((items) => replacePayment(items, refreshed));

        if (refreshed.status === "Completed") {
          router.refresh();
        }
      } catch {
        // Keep polling; the background worker also reconciles pending charges.
        setActivePayment({ ...activePayment });
      }
    }, pollIntervalMs);

    return () => window.clearTimeout(timeoutId);
  }, [activePayment, router]);

  return (
    <>
      {overview.mockMode ? <Notice tone="warning" title="PayChangu mock mode">Payments are simulated here. Mock mode is refused in production.</Notice> : null}

      {activePayment?.status === "Completed" ? (
        <Notice tone="success" title="Payment confirmed">
          {entitlement.planName} is active until {formatDate(entitlement.activeUntil)}.
        </Notice>
      ) : null}

      {activePayment && !isPending && activePayment.status !== "Completed" ? (
        <Notice tone="danger" title="Payment not completed">
          {activePayment.statusMessage ?? "The payment did not go through."}
        </Notice>
      ) : null}

      <section className="plan-grid">
        {[freePlan, ...overview.plans].map((plan) => (
          <PlanCard key={plan.id} plan={plan} entitlement={entitlement} disabled={isPending} onChoose={() => setCheckoutPlan(plan)} />
        ))}
      </section>

      <SectionCard title="Payment history" description="Mobile money charges through PayChangu.">
        {payments.length === 0 ? (
          <EmptyState title="No payments yet">Each charge appears here with its PayChangu reference.</EmptyState>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Plan</th>
                  <th>Amount</th>
                  <th>Paid with</th>
                  <th>Reference</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {payments.map((payment) => (
                  <tr key={payment.id}>
                    <td className="table-muted">{formatDateTime(payment.createdAt)}</td>
                    <td>{overview.plans.find((plan) => plan.id === payment.planId)?.name ?? payment.planId}</td>
                    <td>{formatMwk(payment.amount)}</td>
                    <td className="table-muted">
                      {operatorLabel(overview.operators, payment.operator)} ••{payment.mobileLastFour}
                    </td>
                    <td className="mono table-muted">{payment.txRef.slice(0, 16)}…</td>
                    <td>
                      <PaymentStatusBadge status={payment.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      {checkoutPlan ? (
        <CheckoutDialog
          plan={checkoutPlan}
          operators={overview.operators}
          onClose={() => setCheckoutPlan(null)}
          onStarted={(payment) => {
            setPayments((items) => [payment, ...items]);
            setActivePayment(payment);
            setCheckoutPlan(null);
          }}
        />
      ) : null}

      {activePayment && isPending ? (
        <Dialog title="Approve on your phone" description="This page updates when PayChangu confirms." dismissible={false} onClose={() => undefined}>
          <div className="payment-progress">
            <span className="spinner" aria-hidden="true" />
            <p>
              Enter your PIN for {formatMwk(activePayment.amount)} on the number ending {activePayment.mobileLastFour}.
            </p>
          </div>
          <p className="console-hint mono">{activePayment.txRef}</p>
        </Dialog>
      ) : null}
    </>
  );
}

function PlanCard({ plan, entitlement, disabled, onChoose }: { plan: BillingPlan; entitlement: Entitlement; disabled: boolean; onChoose: () => void }) {
  const current = plan.id === entitlement.planId;
  const classes = ["card", "plan-card", plan.highlighted ? "plan-card-highlighted" : "", current ? "plan-card-current" : ""].filter(Boolean).join(" ");

  return (
    <article className={classes}>
      <div>
        <div className="plan-card-title">
          <h2>{plan.name}</h2>
          {current ? <span className="badge badge-success">Current</span> : null}
        </div>
        <p>{plan.description}</p>
      </div>
      <p className="plan-price">
        <strong>{formatMwk(plan.amountMwk)}</strong>
        <span>{plan.amountMwk === 0 ? "forever" : `per ${plan.periodDays} days`}</span>
      </p>
      <ul className="plan-features">
        {plan.features.map((feature) => (
          <li key={feature}>{feature}</li>
        ))}
      </ul>
      {plan.amountMwk === 0 ? (
        <button type="button" className="btn btn-secondary btn-block" disabled>
          {current ? "Current plan" : "Included"}
        </button>
      ) : (
        <button type="button" className={plan.highlighted ? "btn btn-primary btn-block" : "btn btn-secondary btn-block"} onClick={onChoose} disabled={disabled}>
          {planActionLabel(plan, entitlement)}
        </button>
      )}
    </article>
  );
}

function CheckoutDialog({ plan, operators, onClose, onStarted }: { plan: BillingPlan; operators: BillingOperator[]; onClose: () => void; onStarted: (payment: BillingPayment) => void }) {
  const [operator, setOperator] = useState(operators[0]?.id ?? "");
  const [phone, setPhone] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const selected = operators.find((item) => item.id === operator);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setErrors({});

    try {
      onStarted(await accountRequest<BillingPayment>("billing/checkout", { method: "POST", body: { planId: plan.id, operator, phoneNumber: phone } }));
    } catch (error) {
      setErrors(
        error instanceof AccountApiError
          ? { phoneNumber: error.fieldMessage("phoneNumber") ?? "", operator: error.fieldMessage("operator") ?? "", form: error.problem?.errors ? "" : error.message }
          : { form: "Could not start the payment. Try again." },
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog
      title={`Pay for ${plan.name}`}
      description={`${formatMwk(plan.amountMwk)} for ${plan.periodDays} days.`}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" form="checkout-form" className="btn btn-primary" disabled={submitting || phone.trim().length === 0}>
            {submitting ? "Sending prompt…" : `Pay ${formatMwk(plan.amountMwk)}`}
          </button>
        </>
      }
    >
      <form id="checkout-form" className="form-stack" onSubmit={submit}>
        <fieldset className="console-fieldset">
          <legend className="console-label">Mobile money</legend>
          <div className="choice-grid">
            {operators.map((item) => (
              <label key={item.id} className="choice-row">
                <input type="radio" name="operator" value={item.id} checked={operator === item.id} onChange={() => setOperator(item.id)} />
                {item.label}
              </label>
            ))}
          </div>
          {errors.operator ? <span className="console-field-error">{errors.operator}</span> : null}
        </fieldset>

        <div className="console-field">
          <label className="console-label" htmlFor="checkout-phone">
            Phone number
          </label>
          <input
            id="checkout-phone"
            className="console-input"
            inputMode="tel"
            autoComplete="tel"
            placeholder={selected?.numberPrefix === "8" ? "+265 88x xxx xxx" : "+265 99x xxx xxx"}
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
          />
          {errors.phoneNumber ? (
            <span className="console-field-error">{errors.phoneNumber}</span>
          ) : selected ? (
            <span className="console-hint">
              {selected.label} numbers start with {selected.numberPrefix} after +265.
            </span>
          ) : null}
        </div>

        {errors.form ? <Notice tone="danger">{errors.form}</Notice> : null}
      </form>
    </Dialog>
  );
}
