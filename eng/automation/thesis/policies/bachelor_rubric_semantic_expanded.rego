package bachelor.rubric

import rego.v1

# Expanded Bachelor thesis semantic validator.
#
# This policy intentionally does not parse raw prose/PDF content. It validates a
# structured semantic assessment produced by an upstream reviewer, static extractor,
# or LLM. The original 95-point grading rubric remains separate from the CSAM
# thesis-guideline requirements so that the guideline checks can be used as audit
# gates without silently changing the official point weighting.
#
# Evaluate:
#   opa eval -d bachelor_rubric_semantic_expanded.rego -i input.json "data.bachelor.rubric.report"

# -----------------------------------------------------------------------------
# English translation of the original grading rubric table
# -----------------------------------------------------------------------------

criteria := {
  "problem_definition": {
    "order": 1,
    "title": "Problem Statement and Objective Definition",
    "max_points": 5,
    "description": "The problem statement is clearly and precisely defined and embedded in a scientific context. The objective and the expected measurable result are clearly formulated.",
    "required_checks": [
      "problem_clear_precise",
      "scientific_context_present",
      "objective_explicit",
      "expected_result_measurable"
    ]
  },
  "methodology_solution_approach": {
    "order": 2,
    "title": "Methodology and Solution Approach",
    "max_points": 40,
    "description": "The methodological approach is logically and comprehensibly structured, appropriate to the objective, and the applied methods are correctly and soundly implemented. The methodology is technically and discipline-specifically appropriate, justified, and scientifically defensible.",
    "required_checks": [
      "method_sequence_logical",
      "methods_match_objective",
      "methods_correctly_applied",
      "method_selection_justified",
      "discipline_specific_fit_explained",
      "scientifically_defensible"
    ]
  },
  "results_discussion": {
    "order": 3,
    "title": "Results and Discussion",
    "max_points": 40,
    "description": "The quality of the solution is sufficient in relation to the objective. The results are well-founded, analyzed, and interpreted in relation to the objective. The discussion reflects the relevance and limitations of the results critically and is logically structured.",
    "required_checks": [
      "solution_quality_sufficient",
      "results_well_founded",
      "results_analyzed",
      "results_interpreted_against_objective",
      "relevance_discussed",
      "limitations_discussed",
      "critical_logical_discussion"
    ]
  },
  "structure_organization": {
    "order": 4,
    "title": "Structure and Organization",
    "max_points": 5,
    "description": "The work follows a logical, clear structure and has a consistent red thread. The table of contents, figures, tables, and text are prepared according to the usual valid scientific guidelines of FH Technikum Wien.",
    "required_checks": [
      "logical_structure",
      "clear_structure",
      "red_thread_consistent",
      "toc_figures_tables_text_guideline_compliant"
    ]
  },
  "style_expression": {
    "order": 5,
    "title": "Style and Expression",
    "max_points": 5,
    "description": "The language is precise, technically correct, and fulfills the requirements for gender-sensitive language according to the applicable FH Technikum Wien guidelines.",
    "required_checks": [
      "technical_language_precise",
      "technical_terms_correct",
      "gender_sensitive_language_compliant",
      "fh_language_guidelines_met"
    ]
  },
  "citation_sources": {
    "order": 6,
    "title": "Citation Rules and References",
    "max_points": 5,
    "description": "The scope, quality, and currentness of the processed sources are appropriate and represent the current state of research on the topic. The prescribed citation rules are consistently and correctly applied.",
    "required_checks": [
      "source_scope_adequate",
      "source_quality_adequate",
      "sources_current",
      "state_of_research_represented",
      "citation_rules_consistent",
      "citation_rules_correct"
    ]
  }
}

# -----------------------------------------------------------------------------
# CSAM thesis-guideline semantic requirements
# -----------------------------------------------------------------------------

# Guideline statuses expected from the upstream semantic assessor:
# - satisfied: the requirement is met and evidence supports it.
# - partially_satisfied: some evidence exists, but the requirement is incomplete.
# - not_satisfied: the requirement is materially missing or contradicted.
# - not_applicable: the requirement does not apply to this thesis/program/topic.
# - unknown: the assessor cannot determine the status from the available evidence.

guideline_requirements := {
  "engineering_skills_evidenced": {
    "order": 1,
    "title": "Evidence of engineering skills",
    "group": "general_goals",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "engineering_process_documented",
      "engineering_decisions_justified"
    ]
  },
  "non_trivial_artifact_documented": {
    "order": 2,
    "title": "Development and result of a non-trivial artifact are documented",
    "group": "general_goals",
    "rubric_criterion": "results_discussion",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "artifact_type_identified",
      "artifact_non_triviality_argued",
      "development_result_documented"
    ]
  },
  "concrete_need_bwi": {
    "order": 3,
    "title": "Concrete need for BWI thesis",
    "group": "general_goals",
    "rubric_criterion": "problem_definition",
    "severity": "gate",
    "programs": [
      "BWI"
    ],
    "weight": 1,
    "required_checks": [
      "concrete_need_identified",
      "need_linked_to_work_or_internship_or_practice"
    ]
  },
  "potential_need_bif_bid": {
    "order": 4,
    "title": "Potential need or use for BIF/BID thesis",
    "group": "general_goals",
    "rubric_criterion": "problem_definition",
    "severity": "gate",
    "programs": [
      "BIF",
      "BID"
    ],
    "weight": 1,
    "required_checks": [
      "potential_need_or_use_identified",
      "need_or_use_is_plausible"
    ]
  },
  "solution_not_already_existing": {
    "order": 5,
    "title": "Solution does not already exist",
    "group": "general_goals",
    "rubric_criterion": "problem_definition",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "existing_solutions_checked",
      "novel_development_need_argued"
    ]
  },
  "standard_software_not_sufficient": {
    "order": 6,
    "title": "Typical approaches or standard software cannot be used as-is",
    "group": "general_goals",
    "rubric_criterion": "problem_definition",
    "severity": "gate",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "standard_software_considered",
      "standard_software_limitations_argued"
    ]
  },
  "structured_development_process": {
    "order": 7,
    "title": "Development process is well-structured",
    "group": "general_goals",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "process_phases_defined",
      "phases_build_on_each_other",
      "process_is_traceable"
    ]
  },
  "software_lifecycle_phases_covered": {
    "order": 8,
    "title": "Software-development phases are covered when applicable",
    "group": "general_goals",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "requirements_engineering_covered",
      "specification_or_design_covered",
      "implementation_covered",
      "testing_covered"
    ]
  },
  "methods_sound_and_validated": {
    "order": 9,
    "title": "Methods have sound background and are validated in practice",
    "group": "general_goals",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "methods_have_practice_or_science_background",
      "methods_are_validated_in_practice",
      "best_practices_documented"
    ]
  },
  "methods_tools_researched_and_argued": {
    "order": 10,
    "title": "Choice of methods and tools is researched and argued",
    "group": "general_goals",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "method_options_researched",
      "tool_options_researched",
      "alternatives_discussed",
      "selection_criteria_used",
      "choices_argued_beyond_company_constraints"
    ]
  },
  "artifact_evaluated": {
    "order": 11,
    "title": "Results and artifact are evaluated",
    "group": "general_goals",
    "rubric_criterion": "results_discussion",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "evaluation_against_original_goals",
      "quality_criteria_used",
      "customer_acceptance_not_only_evidence"
    ]
  },
  "artifact_tested_and_analyzed": {
    "order": 12,
    "title": "Artifact is tested and analyzed",
    "group": "general_goals",
    "rubric_criterion": "results_discussion",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "system_or_integration_tests_present",
      "performance_or_usability_or_reliability_analyzed",
      "test_results_interpreted"
    ]
  },
  "formal_outer_structure_present": {
    "order": 20,
    "title": "Outer thesis structure is present",
    "group": "structure",
    "rubric_criterion": "structure_organization",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "title_author_affiliation_present",
      "abstract_present",
      "keywords_present",
      "bibliography_present",
      "appendix_present"
    ]
  },
  "main_word_count_reasonable": {
    "order": 21,
    "title": "Main paper length is around the expected 5,000-6,000 words/about 6,000 words",
    "group": "structure",
    "rubric_criterion": "structure_organization",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "main_word_count_reported",
      "main_word_count_reasonable_for_topic"
    ]
  },
  "four_main_parts_present": {
    "order": 22,
    "title": "Four main parts are present",
    "group": "structure",
    "rubric_criterion": "structure_organization",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "introduction_present",
      "methodology_present",
      "solution_present",
      "discussion_present"
    ]
  },
  "section_distribution_plausible": {
    "order": 23,
    "title": "Section distribution is plausible for the topic",
    "group": "structure",
    "rubric_criterion": "structure_organization",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "introduction_roughly_15_percent",
      "methodology_roughly_20_percent",
      "solution_roughly_50_percent",
      "discussion_roughly_15_percent",
      "deviations_explained_if_material"
    ]
  },
  "appendix_used_correctly": {
    "order": 24,
    "title": "Appendix contains details but does not replace the main paper",
    "group": "structure",
    "rubric_criterion": "structure_organization",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "appendix_contains_details",
      "main_paper_self_contained",
      "important_content_not_only_in_appendix",
      "main_part_provides_summary"
    ]
  },
  "introduction_motivation_and_tasks": {
    "order": 30,
    "title": "Introduction contains motivation and concrete tasks",
    "group": "introduction",
    "rubric_criterion": "problem_definition",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "motivation_need_or_use_explained",
      "specific_problem_context_explained",
      "concrete_goals_or_tasks_stated",
      "expected_result_stated"
    ]
  },
  "methodology_development_rationale": {
    "order": 40,
    "title": "Methodology explains how and why the solution was developed",
    "group": "methodology",
    "rubric_criterion": "methodology_solution_approach",
    "severity": "gate",
    "programs": [],
    "weight": 3,
    "required_checks": [
      "ideal_solution_considered_independent_of_constraints",
      "requirements_gathering_explained",
      "development_process_described",
      "process_choice_explained",
      "process_steps_have_input_output",
      "final_artifact_testing_explained",
      "tools_described",
      "tool_choice_based_on_selection_criteria",
      "outcome_type_stated"
    ]
  },
  "solution_software_content": {
    "order": 50,
    "title": "Solution describes the artifact from user and architecture perspectives",
    "group": "solution",
    "rubric_criterion": "results_discussion",
    "severity": "gate",
    "programs": [],
    "weight": 3,
    "required_checks": [
      "user_requirements_present",
      "functionality_from_user_perspective",
      "ui_representative_screenshots_if_applicable",
      "architecture_components_interfaces_present",
      "component_design_present_if_applicable"
    ]
  },
  "solution_communication_quality": {
    "order": 51,
    "title": "Solution uses meaningful tables, figures, and standard notations where useful",
    "group": "solution",
    "rubric_criterion": "structure_organization",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "tables_figures_have_information_value",
      "tables_figures_explained_in_text",
      "standard_notations_used_where_applicable",
      "code_and_extra_screenshots_in_appendix"
    ]
  },
  "main_paper_self_contained": {
    "order": 52,
    "title": "Main paper is understandable without reading the appendix",
    "group": "solution",
    "rubric_criterion": "structure_organization",
    "severity": "gate",
    "programs": [],
    "weight": 2,
    "required_checks": [
      "main_text_explains_core_solution",
      "appendix_is_supplementary",
      "reader_not_forced_to_appendix_for_core_understanding"
    ]
  },
  "discussion_quality": {
    "order": 60,
    "title": "Discussion critically investigates the quality of the solution",
    "group": "discussion",
    "rubric_criterion": "results_discussion",
    "severity": "gate",
    "programs": [],
    "weight": 3,
    "required_checks": [
      "potentials_discussed",
      "limitations_discussed",
      "possible_improvements_discussed",
      "transfer_to_other_contexts_discussed",
      "generality_discussed",
      "lessons_from_development_process_discussed"
    ]
  },
  "appendix_details_present": {
    "order": 70,
    "title": "Appendix contains detailed supporting material",
    "group": "appendix",
    "rubric_criterion": "structure_organization",
    "severity": "warning",
    "programs": [],
    "weight": 1,
    "required_checks": [
      "code_or_implementation_details_present_if_applicable",
      "interview_transcripts_or_raw_material_present_if_applicable",
      "additional_analyses_present_if_applicable",
      "appendix_material_relevant_to_grade"
    ]
  }
}

status_scores := {
  "satisfied": 1,
  "partially_satisfied": 0.5,
  "not_satisfied": 0,
  "not_applicable": 0,
  "unknown": 0
}

negative_statuses := {
  "partially_satisfied": true,
  "not_satisfied": true,
  "unknown": true
}

blocking_statuses := {
  "partially_satisfied": true,
  "not_satisfied": true,
  "unknown": true
}

programs := {
  "BWI": "Bachelor Wirtschaftsinformatik",
  "BIF": "Bachelor Informatik",
  "BID": "Bachelor Informatik Dual",
  "OTHER": "Other or not course-specific",
  "UNKNOWN": "Unknown"
}

required_evidence_fields := ["section", "claim", "quote", "confidence"]

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

has_key(obj, key) if {
  is_object(obj)
  _ := obj[key]
}

valid_percent(value) if {
  is_number(value)
  value >= 0
  value <= 100
}

valid_confidence(value) if {
  is_number(value)
  value >= 0
  value <= 1
}

valid_status(value) if {
  is_string(value)
  _ := status_scores[value]
}

valid_program(value) if {
  is_string(value)
  _ := programs[value]
}

criterion_ids contains id if {
  _ := criteria[id]
}

guideline_ids contains id if {
  _ := guideline_requirements[id]
}

# -----------------------------------------------------------------------------
# Safe input access and configurable policy defaults
# -----------------------------------------------------------------------------

raw_assessment := object.get(input, "assessment", {})
assessment := raw_assessment if {
  is_object(raw_assessment)
} else := {}

raw_assessment_criteria := object.get(assessment, "criteria", {})
assessment_criteria := raw_assessment_criteria if {
  is_object(raw_assessment_criteria)
} else := {}

raw_guideline_checks := object.get(assessment, "guideline_checks", {})
guideline_checks := raw_guideline_checks if {
  is_object(raw_guideline_checks)
} else := {}

raw_thesis := object.get(input, "thesis", {})
thesis := raw_thesis if {
  is_object(raw_thesis)
} else := {}

thesis_program := value if {
  raw := object.get(thesis, "program", "UNKNOWN")
  valid_program(raw)
  value := raw
} else := "UNKNOWN"

raw_policy := object.get(input, "policy", {})
policy := raw_policy if {
  is_object(raw_policy)
} else := {}

min_total_percent := value if {
  value := object.get(policy, "min_total_percent", 60)
  valid_percent(value)
} else := 60

min_guideline_percent := value if {
  value := object.get(policy, "min_guideline_percent", 70)
  valid_percent(value)
} else := 70

min_evidence_confidence := value if {
  value := object.get(policy, "min_evidence_confidence", 0.70)
  valid_confidence(value)
} else := 0.70

allowed_percent_deviation := value if {
  value := object.get(policy, "allowed_percent_deviation", 25)
  valid_percent(value)
} else := 25

guideline_checks_required := value if {
  value := object.get(policy, "require_guideline_checks", true)
  is_boolean(value)
} else := true

guideline_gates_required := value if {
  value := object.get(policy, "require_guideline_gates", false)
  is_boolean(value)
} else := false

criterion_item(id) := item if {
  item := object.get(assessment_criteria, id, {})
}

guideline_item(id) := item if {
  item := object.get(guideline_checks, id, {})
}

fulfillment_percent(id) := percent if {
  item := criterion_item(id)
  raw := object.get(item, "fulfillment_percent", 0)
  valid_percent(raw)
  percent := raw
} else := 0

check_value(id, check_key) := value if {
  checks := object.get(criterion_item(id), "checks", {})
  raw := object.get(checks, check_key, false)
  is_boolean(raw)
  value := raw
} else := false

guideline_check_value(id, check_key) := value if {
  checks := object.get(guideline_item(id), "checks", {})
  raw := object.get(checks, check_key, false)
  is_boolean(raw)
  value := raw
} else := false

guideline_status(id) := status if {
  raw := object.get(guideline_item(id), "status", "unknown")
  valid_status(raw)
  status := raw
} else := "unknown"

guideline_status_score(id) := score if {
  score := status_scores[guideline_status(id)]
}

applies_to_program(id) if {
  count(guideline_requirements[id].programs) == 0
}

applies_to_program(id) if {
  some program in guideline_requirements[id].programs
  program == thesis_program
}

guideline_applicable(id) := true if {
  applies_to_program(id)
} else := false

guideline_applicable_ids contains id if {
  some id in guideline_ids
  applies_to_program(id)
}

# -----------------------------------------------------------------------------
# Scoring
# -----------------------------------------------------------------------------

total_max_points := sum([criteria[id].max_points | id = criterion_ids[_]])

criterion_score(id) := score if {
  score := criteria[id].max_points * fulfillment_percent(id) / 100
}

true_check_count(id) := count([check_key |
  some check_key in criteria[id].required_checks
  check_value(id, check_key)
])

check_based_percent(id) := percent if {
  percent := true_check_count(id) * 100 / count(criteria[id].required_checks)
}

percent_deviation(id) := deviation if {
  fulfillment_percent(id) >= check_based_percent(id)
  deviation := fulfillment_percent(id) - check_based_percent(id)
}

percent_deviation(id) := deviation if {
  check_based_percent(id) > fulfillment_percent(id)
  deviation := check_based_percent(id) - fulfillment_percent(id)
}

points_by_criterion := {id: {
  "order": criteria[id].order,
  "title": criteria[id].title,
  "max_points": criteria[id].max_points,
  "fulfillment_percent": fulfillment_percent(id),
  "awarded_points": criterion_score(id),
  "check_based_percent": check_based_percent(id)
} | id = criterion_ids[_]}

total_points := sum([criterion_score(id) | id = criterion_ids[_]])

total_percent := total_points * 100 / total_max_points

guideline_total_weight := sum([guideline_requirements[id].weight | id = guideline_applicable_ids[_]])

guideline_points := sum([(guideline_requirements[id].weight * guideline_status_score(id)) | id = guideline_applicable_ids[_]])

guideline_percent := percent if {
  guideline_total_weight > 0
  percent := guideline_points * 100 / guideline_total_weight
} else := 0

guideline_true_check_count(id) := count([check_key |
  some check_key in guideline_requirements[id].required_checks
  guideline_check_value(id, check_key)
])

guideline_check_based_percent(id) := percent if {
  count(guideline_requirements[id].required_checks) > 0
  percent := guideline_true_check_count(id) * 100 / count(guideline_requirements[id].required_checks)
} else := 0

guideline_by_requirement := {id: {
  "order": guideline_requirements[id].order,
  "title": guideline_requirements[id].title,
  "group": guideline_requirements[id].group,
  "rubric_criterion": guideline_requirements[id].rubric_criterion,
  "severity": guideline_requirements[id].severity,
  "programs": guideline_requirements[id].programs,
  "applicable": guideline_applicable(id),
  "weight": guideline_requirements[id].weight,
  "status": guideline_status(id),
  "status_score": guideline_status_score(id),
  "weighted_points": guideline_requirements[id].weight * guideline_status_score(id),
  "check_based_percent": guideline_check_based_percent(id)
} | id = guideline_ids[_]}

# -----------------------------------------------------------------------------
# Schema and integrity validation errors
# -----------------------------------------------------------------------------

validation_errors contains error if {
  raw := object.get(input, "assessment", null)
  not is_object(raw)
  error := {
    "severity": "error",
    "id": "missing_or_invalid_assessment",
    "message": "input.assessment must be an object."
  }
}

validation_errors contains error if {
  raw := object.get(assessment, "criteria", null)
  not is_object(raw)
  error := {
    "severity": "error",
    "id": "missing_or_invalid_assessment_criteria",
    "message": "input.assessment.criteria must be an object keyed by rubric criterion id."
  }
}

validation_errors contains error if {
  some id in criterion_ids
  not has_key(assessment_criteria, id)
  error := {
    "severity": "error",
    "id": "missing_criterion",
    "criterion": id,
    "message": sprintf("Missing assessment for required criterion '%s'.", [id])
  }
}

validation_errors contains error if {
  some id
  _ := assessment_criteria[id]
  not has_key(criteria, id)
  error := {
    "severity": "error",
    "id": "unknown_criterion",
    "criterion": id,
    "message": sprintf("Unknown criterion '%s'. Use only the rubric criterion ids defined in data.bachelor.rubric.criteria.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  item := criterion_item(id)
  not has_key(item, "fulfillment_percent")
  error := {
    "severity": "error",
    "id": "missing_fulfillment_percent",
    "criterion": id,
    "message": sprintf("Criterion '%s' must provide fulfillment_percent from 0 to 100.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  item := criterion_item(id)
  has_key(item, "fulfillment_percent")
  percent := item.fulfillment_percent
  not valid_percent(percent)
  error := {
    "severity": "error",
    "id": "invalid_fulfillment_percent",
    "criterion": id,
    "value": percent,
    "message": sprintf("Criterion '%s' has an invalid fulfillment_percent; expected a number from 0 to 100.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  checks := object.get(criterion_item(id), "checks", {})
  not is_object(checks)
  error := {
    "severity": "error",
    "id": "invalid_checks_object",
    "criterion": id,
    "message": sprintf("Criterion '%s' must provide checks as an object of boolean semantic checks.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  checks := object.get(criterion_item(id), "checks", {})
  is_object(checks)
  some check_key in criteria[id].required_checks
  not has_key(checks, check_key)
  error := {
    "severity": "error",
    "id": "missing_semantic_check",
    "criterion": id,
    "check": check_key,
    "message": sprintf("Criterion '%s' is missing semantic check '%s'.", [id, check_key])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  checks := object.get(criterion_item(id), "checks", {})
  is_object(checks)
  some check_key in criteria[id].required_checks
  has_key(checks, check_key)
  not is_boolean(checks[check_key])
  error := {
    "severity": "error",
    "id": "invalid_semantic_check_type",
    "criterion": id,
    "check": check_key,
    "message": sprintf("Semantic check '%s' for criterion '%s' must be boolean.", [check_key, id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  not is_array(evidence)
  error := {
    "severity": "error",
    "id": "invalid_evidence_array",
    "criterion": id,
    "message": sprintf("Criterion '%s' must provide evidence as an array.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  is_array(evidence)
  count(evidence) == 0
  error := {
    "severity": "error",
    "id": "missing_evidence",
    "criterion": id,
    "message": sprintf("Criterion '%s' must provide at least one evidence item.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  not is_object(ev)
  error := {
    "severity": "error",
    "id": "invalid_evidence_item",
    "criterion": id,
    "message": sprintf("Criterion '%s' has an evidence item that is not an object.", [id])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  some field in required_evidence_fields
  not has_key(ev, field)
  error := {
    "severity": "error",
    "id": "missing_evidence_field",
    "criterion": id,
    "field": field,
    "message": sprintf("Criterion '%s' has an evidence item missing field '%s'.", [id, field])
  }
}

validation_errors contains error if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  has_key(ev, "confidence")
  not valid_confidence(ev.confidence)
  error := {
    "severity": "error",
    "id": "invalid_evidence_confidence",
    "criterion": id,
    "value": ev.confidence,
    "message": sprintf("Criterion '%s' has invalid evidence confidence; expected a number from 0 to 1.", [id])
  }
}

validation_errors contains error if {
  raw := object.get(thesis, "program", null)
  raw != null
  not valid_program(raw)
  error := {
    "severity": "error",
    "id": "invalid_thesis_program",
    "value": raw,
    "allowed_values": object.keys(programs),
    "message": "input.thesis.program must be one of BWI, BIF, BID, OTHER, UNKNOWN."
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  raw := object.get(assessment, "guideline_checks", null)
  not is_object(raw)
  error := {
    "severity": "error",
    "id": "missing_or_invalid_guideline_checks",
    "message": "input.assessment.guideline_checks must be an object when policy.require_guideline_checks is true."
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  not has_key(guideline_checks, id)
  error := {
    "severity": "error",
    "id": "missing_guideline_requirement",
    "requirement": id,
    "message": sprintf("Missing assessment for applicable guideline requirement '%s'.", [id])
  }
}

validation_errors contains error if {
  some id
  _ := guideline_checks[id]
  not has_key(guideline_requirements, id)
  error := {
    "severity": "error",
    "id": "unknown_guideline_requirement",
    "requirement": id,
    "message": sprintf("Unknown guideline requirement '%s'. Use only ids defined in data.bachelor.rubric.guideline_requirements.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  item := guideline_item(id)
  not has_key(item, "status")
  error := {
    "severity": "error",
    "id": "missing_guideline_status",
    "requirement": id,
    "message": sprintf("Guideline requirement '%s' must provide status.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  item := guideline_item(id)
  has_key(item, "status")
  not valid_status(item.status)
  error := {
    "severity": "error",
    "id": "invalid_guideline_status",
    "requirement": id,
    "value": item.status,
    "allowed_values": object.keys(status_scores),
    "message": sprintf("Guideline requirement '%s' has invalid status.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  checks := object.get(guideline_item(id), "checks", {})
  not is_object(checks)
  error := {
    "severity": "error",
    "id": "invalid_guideline_checks_object",
    "requirement": id,
    "message": sprintf("Guideline requirement '%s' must provide checks as an object of boolean semantic checks.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  checks := object.get(guideline_item(id), "checks", {})
  is_object(checks)
  some check_key in guideline_requirements[id].required_checks
  not has_key(checks, check_key)
  error := {
    "severity": "error",
    "id": "missing_guideline_semantic_check",
    "requirement": id,
    "check": check_key,
    "message": sprintf("Guideline requirement '%s' is missing semantic check '%s'.", [id, check_key])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  checks := object.get(guideline_item(id), "checks", {})
  is_object(checks)
  some check_key in guideline_requirements[id].required_checks
  has_key(checks, check_key)
  not is_boolean(checks[check_key])
  error := {
    "severity": "error",
    "id": "invalid_guideline_semantic_check_type",
    "requirement": id,
    "check": check_key,
    "message": sprintf("Guideline semantic check '%s' for requirement '%s' must be boolean.", [check_key, id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  evidence := object.get(guideline_item(id), "evidence", [])
  not is_array(evidence)
  error := {
    "severity": "error",
    "id": "invalid_guideline_evidence_array",
    "requirement": id,
    "message": sprintf("Guideline requirement '%s' must provide evidence as an array.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  guideline_status(id) != "not_applicable"
  evidence := object.get(guideline_item(id), "evidence", [])
  is_array(evidence)
  count(evidence) == 0
  error := {
    "severity": "error",
    "id": "missing_guideline_evidence",
    "requirement": id,
    "message": sprintf("Guideline requirement '%s' must provide at least one evidence item unless status is not_applicable.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  evidence := object.get(guideline_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  not is_object(ev)
  error := {
    "severity": "error",
    "id": "invalid_guideline_evidence_item",
    "requirement": id,
    "message": sprintf("Guideline requirement '%s' has an evidence item that is not an object.", [id])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  evidence := object.get(guideline_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  some field in required_evidence_fields
  not has_key(ev, field)
  error := {
    "severity": "error",
    "id": "missing_guideline_evidence_field",
    "requirement": id,
    "field": field,
    "message": sprintf("Guideline requirement '%s' has an evidence item missing field '%s'.", [id, field])
  }
}

validation_errors contains error if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  evidence := object.get(guideline_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  has_key(ev, "confidence")
  not valid_confidence(ev.confidence)
  error := {
    "severity": "error",
    "id": "invalid_guideline_evidence_confidence",
    "requirement": id,
    "value": ev.confidence,
    "message": sprintf("Guideline requirement '%s' has invalid evidence confidence; expected a number from 0 to 1.", [id])
  }
}

# -----------------------------------------------------------------------------
# Semantic findings: audit-relevant warnings and optional hard guideline gates
# -----------------------------------------------------------------------------

semantic_findings contains finding if {
  some id in criterion_ids
  checks := object.get(criterion_item(id), "checks", {})
  is_object(checks)
  some check_key in criteria[id].required_checks
  has_key(checks, check_key)
  checks[check_key] == false
  finding := {
    "severity": "warning",
    "id": "rubric_semantic_check_not_satisfied",
    "criterion": id,
    "check": check_key,
    "message": sprintf("Semantic check '%s' is not satisfied for criterion '%s'.", [check_key, id])
  }
}

semantic_findings contains finding if {
  some id in criterion_ids
  evidence := object.get(criterion_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  has_key(ev, "confidence")
  valid_confidence(ev.confidence)
  ev.confidence < min_evidence_confidence
  finding := {
    "severity": "warning",
    "id": "low_rubric_evidence_confidence",
    "criterion": id,
    "confidence": ev.confidence,
    "min_confidence": min_evidence_confidence,
    "message": sprintf("Evidence confidence for criterion '%s' is below the configured threshold.", [id])
  }
}

semantic_findings contains finding if {
  some id in criterion_ids
  percent_deviation(id) > allowed_percent_deviation
  finding := {
    "severity": "warning",
    "id": "rubric_percent_semantic_check_deviation",
    "criterion": id,
    "fulfillment_percent": fulfillment_percent(id),
    "check_based_percent": check_based_percent(id),
    "allowed_deviation": allowed_percent_deviation,
    "message": sprintf("Criterion '%s' fulfillment_percent differs materially from the boolean semantic checks.", [id])
  }
}

semantic_findings contains finding if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  status := guideline_status(id)
  negative_statuses[status]
  finding := {
    "severity": guideline_requirements[id].severity,
    "id": "guideline_requirement_not_fully_satisfied",
    "requirement": id,
    "status": status,
    "group": guideline_requirements[id].group,
    "rubric_criterion": guideline_requirements[id].rubric_criterion,
    "message": sprintf("Guideline requirement '%s' is not fully satisfied.", [id])
  }
}

semantic_findings contains finding if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  checks := object.get(guideline_item(id), "checks", {})
  is_object(checks)
  some check_key in guideline_requirements[id].required_checks
  has_key(checks, check_key)
  checks[check_key] == false
  finding := {
    "severity": "warning",
    "id": "guideline_semantic_check_not_satisfied",
    "requirement": id,
    "check": check_key,
    "group": guideline_requirements[id].group,
    "message": sprintf("Guideline semantic check '%s' is not satisfied for requirement '%s'.", [check_key, id])
  }
}

semantic_findings contains finding if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  evidence := object.get(guideline_item(id), "evidence", [])
  is_array(evidence)
  some ev in evidence
  is_object(ev)
  has_key(ev, "confidence")
  valid_confidence(ev.confidence)
  ev.confidence < min_evidence_confidence
  finding := {
    "severity": "warning",
    "id": "low_guideline_evidence_confidence",
    "requirement": id,
    "confidence": ev.confidence,
    "min_confidence": min_evidence_confidence,
    "message": sprintf("Evidence confidence for guideline requirement '%s' is below the configured threshold.", [id])
  }
}

semantic_findings contains finding if {
  thesis_program == "UNKNOWN"
  finding := {
    "severity": "warning",
    "id": "unknown_thesis_program",
    "message": "input.thesis.program is UNKNOWN, so BWI/BIF/BID-specific requirements cannot be applied precisely."
  }
}

semantic_findings contains finding if {
  word_count := object.get(thesis, "main_word_count", null)
  is_number(word_count)
  word_count < 5000
  finding := {
    "severity": "warning",
    "id": "main_word_count_below_typical_range",
    "value": word_count,
    "message": "Main-part word count is below the typical CSAM guideline range of about 5,000-6,000 words. Topic-specific deviations may still be acceptable."
  }
}

semantic_findings contains finding if {
  word_count := object.get(thesis, "main_word_count", null)
  is_number(word_count)
  word_count > 6500
  finding := {
    "severity": "warning",
    "id": "main_word_count_above_typical_range",
    "value": word_count,
    "message": "Main-part word count is above the typical CSAM guideline range of about 5,000-6,000 words. Topic-specific deviations may still be acceptable."
  }
}

blocking_guideline_findings contains finding if {
  guideline_checks_required == true
  some id in guideline_applicable_ids
  guideline_requirements[id].severity == "gate"
  status := guideline_status(id)
  blocking_statuses[status]
  finding := {
    "severity": "gate",
    "id": "blocking_guideline_gate_not_satisfied",
    "requirement": id,
    "status": status,
    "message": sprintf("Blocking guideline gate '%s' is not satisfied.", [id])
  }
}

# -----------------------------------------------------------------------------
# Decision/report
# -----------------------------------------------------------------------------

default allow := false

allow if {
  meets_threshold
  guideline_gate_mode_passes
}

valid_assessment := count(validation_errors) == 0

default meets_threshold := false

meets_threshold if {
  valid_assessment
  total_percent >= min_total_percent
}

default guideline_threshold_met := false

guideline_threshold_met if {
  guideline_percent >= min_guideline_percent
}

default guideline_gate_mode_passes := false

guideline_gate_mode_passes if {
  guideline_gates_required == false
}

guideline_gate_mode_passes if {
  guideline_gates_required == true
  count(blocking_guideline_findings) == 0
  guideline_threshold_met
}

rubric := {
  "name": "Bachelor thesis assessment rubric",
  "source_language": "German",
  "translated_language": "English",
  "total_max_points": total_max_points,
  "criteria": criteria
}

guideline_policy := {
  "name": "CSAM Bachelor thesis semantic guideline requirements",
  "purpose": "Semantic audit gates and evidence checks; not a replacement for human grading.",
  "program": thesis_program,
  "requirements": guideline_requirements
}

report := {
  "allow": allow,
  "valid_assessment": valid_assessment,
  "meets_rubric_threshold": meets_threshold,
  "guideline_gate_mode_passes": guideline_gate_mode_passes,
  "configured_thresholds": {
    "min_total_percent": min_total_percent,
    "min_guideline_percent": min_guideline_percent,
    "min_evidence_confidence": min_evidence_confidence,
    "allowed_percent_deviation": allowed_percent_deviation,
    "require_guideline_checks": guideline_checks_required,
    "require_guideline_gates": guideline_gates_required
  },
  "score": {
    "total_max_points": total_max_points,
    "total_points": total_points,
    "total_percent": total_percent,
    "by_criterion": points_by_criterion
  },
  "guidelines": {
    "program": thesis_program,
    "total_weight": guideline_total_weight,
    "weighted_points": guideline_points,
    "percent": guideline_percent,
    "threshold_met": guideline_threshold_met,
    "blocking_findings": blocking_guideline_findings,
    "by_requirement": guideline_by_requirement
  },
  "validation_errors": validation_errors,
  "semantic_findings": semantic_findings,
  "rubric": rubric,
  "guideline_policy": guideline_policy
}
