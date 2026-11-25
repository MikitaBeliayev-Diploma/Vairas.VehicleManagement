CREATE TABLE command_request (
 request_id uuid PRIMARY KEY, school_id uuid NOT NULL, actor_user_id uuid NOT NULL,
 idempotency_key varchar(128) NOT NULL CHECK(length(idempotency_key) BETWEEN 8 AND 128),
 command_name varchar(80) NOT NULL, fingerprint varchar(64) NOT NULL CHECK(fingerprint ~ '^[0-9a-f]{64}$'),
 vehicle_id uuid NOT NULL, response_json text, created_at timestamptz NOT NULL,
 UNIQUE(school_id,actor_user_id,command_name,idempotency_key),
 FOREIGN KEY(vehicle_id,school_id) REFERENCES vehicle(vehicle_id,school_id) ON DELETE RESTRICT
);
CREATE TABLE vehicle_state_change_operation (
 operation_id uuid PRIMARY KEY, school_id uuid NOT NULL, vehicle_id uuid NOT NULL, actor_user_id uuid NOT NULL, branch_id uuid NOT NULL,
 operation_type varchar(32) NOT NULL CHECK(operation_type IN ('Deactivate','Reactivate')),
 status varchar(32) NOT NULL CHECK(status IN ('Pending','Blocking','SchedulingBlocked','ApplyingVehicleState','ReleasingBlock','Completed','Rejected','NeedsRecovery')),
 resume_status varchar(32) NOT NULL CHECK(resume_status IN ('Pending','Blocking','SchedulingBlocked','ApplyingVehicleState','ReleasingBlock','Completed','Rejected','NeedsRecovery')),
 expected_vehicle_revision bigint NOT NULL CHECK(expected_vehicle_revision>0), fingerprint varchar(64) NOT NULL CHECK(fingerprint ~ '^[0-9a-f]{64}$'),
 reason varchar(1000) NOT NULL CHECK(length(trim(reason))>0), correlation_id varchar(128) NOT NULL,
 block_id uuid, block_operation_id uuid, release_operation_id uuid NOT NULL, result_vehicle_status vm_vehicle_status, error_code varchar(120),
 attempts integer NOT NULL CHECK(attempts>=0), next_attempt_at timestamptz NOT NULL, lease_token uuid, lease_until timestamptz,
 created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL CHECK(updated_at>=created_at),
 CHECK((lease_token IS NULL)=(lease_until IS NULL)), CHECK((block_id IS NULL)=(block_operation_id IS NULL)),
 FOREIGN KEY(vehicle_id,school_id) REFERENCES vehicle(vehicle_id,school_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX vm_one_live_state_change ON vehicle_state_change_operation(school_id,vehicle_id) WHERE status NOT IN ('Completed','Rejected');
CREATE INDEX vm_saga_due ON vehicle_state_change_operation(next_attempt_at,lease_until) WHERE status NOT IN ('Completed','Rejected');
CREATE TABLE outbox_message (
 message_id uuid PRIMARY KEY, school_id uuid NOT NULL, vehicle_id uuid NOT NULL, aggregate_version bigint NOT NULL CHECK(aggregate_version>0),
 event_type varchar(80) NOT NULL CHECK(event_type IN ('VehicleActivated','VehicleDeactivated')), payload_json text NOT NULL,
 correlation_id varchar(128) NOT NULL, occurred_at timestamptz NOT NULL, next_attempt_at timestamptz NOT NULL,
 lease_token uuid, lease_until timestamptz, attempts integer NOT NULL CHECK(attempts>=0), published_at timestamptz,
 CHECK((lease_token IS NULL)=(lease_until IS NULL)), CHECK(published_at IS NULL OR published_at>=occurred_at),
 FOREIGN KEY(vehicle_id,school_id) REFERENCES vehicle(vehicle_id,school_id) ON DELETE RESTRICT
);
CREATE INDEX vm_outbox_due ON outbox_message(next_attempt_at,lease_until) WHERE published_at IS NULL;

CREATE FUNCTION vm_immutable() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public,pg_temp AS $$
BEGIN RAISE EXCEPTION 'Immutable evidence' USING ERRCODE='23514'; END $$;
CREATE TRIGGER vm_receipt_immutable BEFORE UPDATE OR DELETE ON vehicle_operation FOR EACH ROW EXECUTE FUNCTION vm_immutable();
CREATE TRIGGER vm_request_immutable BEFORE UPDATE OR DELETE ON command_request FOR EACH ROW EXECUTE FUNCTION vm_immutable();
CREATE FUNCTION vm_vehicle_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public,pg_temp AS $$
BEGIN
 IF NEW.vehicle_id<>OLD.vehicle_id OR NEW.school_id<>OLD.school_id OR NEW.branch_id<>OLD.branch_id OR NEW.created_at<>OLD.created_at
 OR NEW.revision<>OLD.revision+1 THEN RAISE EXCEPTION 'Vehicle identity or revision conflict' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER vm_vehicle_guard BEFORE UPDATE ON vehicle FOR EACH ROW EXECUTE FUNCTION vm_vehicle_guard();
CREATE FUNCTION vm_saga_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public,pg_temp AS $$
BEGIN
 IF ROW(NEW.operation_id,NEW.school_id,NEW.vehicle_id,NEW.actor_user_id,NEW.branch_id,NEW.operation_type,NEW.expected_vehicle_revision,NEW.fingerprint,NEW.reason,NEW.correlation_id,NEW.release_operation_id,NEW.created_at)
 IS DISTINCT FROM ROW(OLD.operation_id,OLD.school_id,OLD.vehicle_id,OLD.actor_user_id,OLD.branch_id,OLD.operation_type,OLD.expected_vehicle_revision,OLD.fingerprint,OLD.reason,OLD.correlation_id,OLD.release_operation_id,OLD.created_at)
 OR OLD.status IN ('Completed','Rejected') THEN RAISE EXCEPTION 'Immutable Saga intent or terminal result' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER vm_saga_guard BEFORE UPDATE ON vehicle_state_change_operation FOR EACH ROW EXECUTE FUNCTION vm_saga_guard();
CREATE FUNCTION vm_outbox_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public,pg_temp AS $$
BEGIN
 IF ROW(NEW.message_id,NEW.school_id,NEW.vehicle_id,NEW.aggregate_version,NEW.event_type,NEW.payload_json,NEW.correlation_id,NEW.occurred_at)
 IS DISTINCT FROM ROW(OLD.message_id,OLD.school_id,OLD.vehicle_id,OLD.aggregate_version,OLD.event_type,OLD.payload_json,OLD.correlation_id,OLD.occurred_at)
 OR OLD.published_at IS NOT NULL THEN RAISE EXCEPTION 'Immutable outbox content or delivered result' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER vm_outbox_guard BEFORE UPDATE ON outbox_message FOR EACH ROW EXECUTE FUNCTION vm_outbox_guard();

CREATE FUNCTION vm_due_work(is_outbox boolean,batch_size integer)
RETURNS TABLE(school_id uuid,item_id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $$
 SELECT w.school_id,w.item_id FROM (
   SELECT o.school_id,o.message_id AS item_id,o.next_attempt_at FROM public.outbox_message o
   WHERE is_outbox AND o.published_at IS NULL AND o.next_attempt_at<=clock_timestamp() AND (o.lease_until IS NULL OR o.lease_until<=clock_timestamp())
   UNION ALL
   SELECT s.school_id,s.operation_id AS item_id,s.next_attempt_at FROM public.vehicle_state_change_operation s
   WHERE NOT is_outbox AND s.status NOT IN ('Completed','Rejected') AND s.next_attempt_at<=clock_timestamp() AND (s.lease_until IS NULL OR s.lease_until<=clock_timestamp())
 ) w ORDER BY w.next_attempt_at,w.item_id LIMIT LEAST(GREATEST(batch_size,1),100)
$$;
REVOKE ALL ON FUNCTION vm_due_work(boolean,integer) FROM PUBLIC;
CREATE FUNCTION vm_work_health() RETURNS TABLE("PendingSagas" bigint,"RecoverySagas" bigint,"OldestSagaSeconds" double precision,"PendingOutbox" bigint,"OldestOutboxSeconds" double precision)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $$
 SELECT
 (SELECT count(*) FROM public.vehicle_state_change_operation WHERE status NOT IN ('Completed','Rejected')),
 (SELECT count(*) FROM public.vehicle_state_change_operation WHERE status='NeedsRecovery'),
 (SELECT COALESCE(GREATEST(EXTRACT(epoch FROM clock_timestamp()-min(created_at)),0),0)::double precision FROM public.vehicle_state_change_operation WHERE status NOT IN ('Completed','Rejected')),
 (SELECT count(*) FROM public.outbox_message WHERE published_at IS NULL),
 (SELECT COALESCE(GREATEST(EXTRACT(epoch FROM clock_timestamp()-min(occurred_at)),0),0)::double precision FROM public.outbox_message WHERE published_at IS NULL)
$$;
REVOKE ALL ON FUNCTION vm_work_health() FROM PUBLIC;
