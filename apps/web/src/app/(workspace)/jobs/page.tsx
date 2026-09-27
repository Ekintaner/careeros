import { JobsPage } from "@/features/opportunities/lists";
export const metadata = { title: "Jobs" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ companyId?: string; state?: string }>;
}) {
  const query = await searchParams;
  return (
    <JobsPage
      companyId={query.companyId}
      state={
        query.state === "all" || query.state === "archived"
          ? query.state
          : "active"
      }
    />
  );
}
