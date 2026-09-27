import { JobDetail } from "@/features/opportunities/job-detail";
export const metadata = { title: "Job details" };
export default async function Page({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ saved?: string }>;
}) {
  return (
    <JobDetail
      id={(await params).id}
      saved={(await searchParams).saved === "1"}
    />
  );
}
