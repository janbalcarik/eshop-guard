-- F7FindingsApi (change 11, AD 13): live progress over SSE. A trigger only names what changed (ids, never a text); the API
-- listens on one connection per instance and reads the rows itself under RLS of the request that subscribed.
--   eg_run:  <tenant>:<run>:<run_events.id>  (0 = the state or progress of the run changed)
--   eg_shop: <tenant>:<shop>:<entity>:<id>   (entity: proposal, group, publication, question, run)

CREATE FUNCTION checks.notify_run_event() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
BEGIN
  PERFORM pg_notify('eg_run', NEW.tenant_id::text || ':' || NEW.run_id::text || ':' || NEW.id::text);
  RETURN NULL;
END
$$;

CREATE FUNCTION checks.notify_run() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
BEGIN
  PERFORM pg_notify('eg_run', NEW.tenant_id::text || ':' || NEW.id::text || ':0');
  PERFORM pg_notify('eg_shop', NEW.tenant_id::text || ':' || NEW.shop_id::text || ':run:' || NEW.id::text);
  RETURN NULL;
END
$$;

CREATE FUNCTION fixes.notify_shop_entity() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
BEGIN
  PERFORM pg_notify('eg_shop', NEW.tenant_id::text || ':' || NEW.shop_id::text || ':' || TG_ARGV[0] || ':' || NEW.id::text);
  RETURN NULL;
END
$$;

REVOKE ALL ON FUNCTION checks.notify_run_event() FROM PUBLIC;
REVOKE ALL ON FUNCTION checks.notify_run() FROM PUBLIC;
REVOKE ALL ON FUNCTION fixes.notify_shop_entity() FROM PUBLIC;

CREATE TRIGGER tr_run_events_notify AFTER INSERT ON checks.run_events
  FOR EACH ROW EXECUTE FUNCTION checks.notify_run_event();

CREATE TRIGGER tr_runs_notify AFTER UPDATE OF status, progress ON checks.runs
  FOR EACH ROW WHEN (OLD.status IS DISTINCT FROM NEW.status OR OLD.progress IS DISTINCT FROM NEW.progress)
  EXECUTE FUNCTION checks.notify_run();

CREATE TRIGGER tr_fix_proposals_notify AFTER UPDATE OF status, recheck_status ON fixes.fix_proposals
  FOR EACH ROW WHEN (OLD.status IS DISTINCT FROM NEW.status OR OLD.recheck_status IS DISTINCT FROM NEW.recheck_status)
  EXECUTE FUNCTION fixes.notify_shop_entity('proposal');

CREATE TRIGGER tr_fix_groups_notify AFTER UPDATE OF status, recheck_status ON fixes.fix_groups
  FOR EACH ROW WHEN (OLD.status IS DISTINCT FROM NEW.status OR OLD.recheck_status IS DISTINCT FROM NEW.recheck_status)
  EXECUTE FUNCTION fixes.notify_shop_entity('group');

CREATE TRIGGER tr_publications_notify AFTER INSERT OR UPDATE OF status ON fixes.publications
  FOR EACH ROW EXECUTE FUNCTION fixes.notify_shop_entity('publication');

CREATE TRIGGER tr_questions_notify AFTER UPDATE OF status ON checks.questions
  FOR EACH ROW WHEN (OLD.status IS DISTINCT FROM NEW.status)
  EXECUTE FUNCTION fixes.notify_shop_entity('question');
