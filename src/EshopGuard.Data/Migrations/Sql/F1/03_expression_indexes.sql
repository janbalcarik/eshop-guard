-- F1DataModel: e-mail unique without regard to case, only among users that are not deleted.
CREATE UNIQUE INDEX ux_users_email_lower ON iam.users (lower(email)) WHERE deleted_at IS NULL;
