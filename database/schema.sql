CREATE TABLE IF NOT EXISTS analytics_events (
  event_id uuid PRIMARY KEY,
  visitor_id uuid NOT NULL,
  session_id uuid NOT NULL,
  event_name varchar(64) NOT NULL,
  occurred_at_utc timestamptz NOT NULL,
  page text, section text, element text, value text, referrer text, landing_url text,
  utm_source text, utm_medium text, utm_campaign text, utm_content text, utm_term text,
  country text, language text, device_type text, os text, browser text,
  screen_width integer, screen_height integer, experiment_id text, variant_id text
);

CREATE INDEX IF NOT EXISTS ix_analytics_events_session_time ON analytics_events(session_id, occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_analytics_events_name_time ON analytics_events(event_name, occurred_at_utc);

CREATE TABLE IF NOT EXISTS early_access_signups (
  id uuid PRIMARY KEY,
  email varchar(254) NOT NULL,
  created_at_utc timestamptz NOT NULL,
  visitor_id uuid NOT NULL,
  session_id uuid NOT NULL,
  source_section text NOT NULL,
  utm_source text, utm_medium text, utm_campaign text, experiment_id text, variant_id text
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_early_access_signups_email_lower ON early_access_signups(lower(email));
