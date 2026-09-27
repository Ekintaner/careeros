import { JobEditorPage } from "@/features/opportunities/job-editor";
export const metadata = { title: "Edit job" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <JobEditorPage id={(await params).id} />;
}
