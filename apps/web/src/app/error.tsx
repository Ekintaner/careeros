"use client";
export default function ErrorPage({ reset }: { reset: () => void }) {
  return (
    <main className="session-state">
      <h1>We couldn’t open this page</h1>
      <p>Please try again in a moment.</p>
      <button onClick={reset}>Try again</button>
    </main>
  );
}
