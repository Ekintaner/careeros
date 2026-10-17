export interface Requirement {
  skillId: string;
  name: string;
  requirementLevel: "Required" | "Preferred";
  weight: number;
  earnedPoints: number;
  matched: boolean;
  evidenceRecordedAtUtc: string | null;
}
export interface Matching {
  jobId: string;
  jobVersion: number;
  source: "Profile" | "Resume";
  resume: {
    id: string;
    name: string;
    version: number;
    skills: { skillId: string; name: string; recordedAtUtc: string }[];
  } | null;
  result: {
    algorithm: string;
    score: number | null;
    required: {
      matched: number;
      total: number;
      earnedPoints: number;
      possiblePoints: number;
    };
    preferred: {
      matched: number;
      total: number;
      earnedPoints: number;
      possiblePoints: number;
    };
    skills: Requirement[];
  };
}
