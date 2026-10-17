import { AssistancePage } from "@/features/assistance/assistance-page";
export const metadata = { title: "Local AI assistance" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <AssistancePage key={(await params).id} jobId={(await params).id} />;
}
