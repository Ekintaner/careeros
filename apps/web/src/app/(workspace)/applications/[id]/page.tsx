import { ApplicationDetail } from "@/features/applications/detail";
export const metadata = { title: "Application" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <ApplicationDetail id={(await params).id} />;
}
