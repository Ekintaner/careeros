import { ApplicationEditor } from "@/features/applications/editor";
export const metadata = { title: "Edit application" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <ApplicationEditor id={(await params).id} />;
}
