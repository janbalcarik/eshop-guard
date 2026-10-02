DROP TRIGGER IF EXISTS tr_questions_notify ON checks.questions;
DROP TRIGGER IF EXISTS tr_publications_notify ON fixes.publications;
DROP TRIGGER IF EXISTS tr_fix_groups_notify ON fixes.fix_groups;
DROP TRIGGER IF EXISTS tr_fix_proposals_notify ON fixes.fix_proposals;
DROP TRIGGER IF EXISTS tr_runs_notify ON checks.runs;
DROP TRIGGER IF EXISTS tr_run_events_notify ON checks.run_events;
DROP FUNCTION IF EXISTS fixes.notify_shop_entity();
DROP FUNCTION IF EXISTS checks.notify_run();
DROP FUNCTION IF EXISTS checks.notify_run_event();
