export type CandidateSelectionDraft = {
  candidateKey: string;
  destinationId?: string;
  mode?: string;
  optionalCostIds: string[];
  sourceObjectId: string;
  targetChoiceIds: string[];
};
