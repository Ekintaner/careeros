import { ApplicationEditor } from "@/features/applications/editor";
export const metadata = { title: "Track application" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ jobId?: string }>;
}) {
  const { jobId } = await searchParams;
  return (
    <ApplicationEditor jobId={typeof jobId === "string" ? jobId : undefined} />
  );
}
