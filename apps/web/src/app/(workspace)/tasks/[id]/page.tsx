import { PlanningEditor } from "@/features/planning/editor";
import "@/features/planning/planning.css";
export const metadata = { title: "Career tasks" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlanningEditor tasks={true} id={id} />;
}
