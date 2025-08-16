CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Users Table (single word, keep as-is)
CREATE TABLE users (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  name character varying NOT NULL,
  username character varying NOT NULL UNIQUE,
  email character varying NOT NULL UNIQUE,
  password character varying NOT NULL,
  mobile_no character varying NOT NULL UNIQUE,
  created_at timestamp without time zone DEFAULT now(),
  updated_at timestamp without time zone DEFAULT now(),
  CONSTRAINT users_pkey PRIMARY KEY (id)
);

-- Roles Table (single word)
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    guard_name VARCHAR(50) DEFAULT 'admin',
    UNIQUE(name, guard_name),
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now()
);

-- Permissions Table (single word)
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    guard_name VARCHAR(50) DEFAULT 'admin',
    UNIQUE(name, guard_name),
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now()
);

-- Model Roles Table → model_roles
CREATE TABLE model_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_id UUID NOT NULL,
    role_id UUID REFERENCES roles(id) ON DELETE CASCADE,
    model_name VARCHAR(50) DEFAULT 'User',
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now(),
    UNIQUE(model_id, role_id, model_name)
);

-- Model Permissions Table → model_permissions
CREATE TABLE model_permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_id UUID NOT NULL,
    permission_id UUID REFERENCES permissions(id) ON DELETE CASCADE,
    model_name VARCHAR(50) DEFAULT 'User',
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now(),
    UNIQUE(model_id, permission_id, model_name)
);

-- Role Permissions Table → role_permissions
CREATE TABLE role_permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    permission_id UUID REFERENCES permissions(id) ON DELETE CASCADE,
    role_id UUID REFERENCES roles(id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now(),
    UNIQUE(permission_id, role_id)
);

-- Triggers for updated_at
CREATE TRIGGER set_users_updated_at
BEFORE UPDATE ON users
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

CREATE TRIGGER set_roles_updated_at
BEFORE UPDATE ON roles
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

CREATE TRIGGER set_permissions_updated_at
BEFORE UPDATE ON permissions
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

CREATE TRIGGER set_model_roles_updated_at
BEFORE UPDATE ON model_roles
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

CREATE TRIGGER set_model_permissions_updated_at
BEFORE UPDATE ON model_permissions
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

CREATE TRIGGER set_role_permissions_updated_at
BEFORE UPDATE ON role_permissions
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

-- Indexes for performance
CREATE INDEX idx_model_roles_model_id ON model_roles(model_id);
CREATE INDEX idx_model_roles_role_id ON model_roles(role_id);

CREATE INDEX idx_model_permissions_model_id ON model_permissions(model_id);
CREATE INDEX idx_model_permissions_permission_id ON model_permissions(permission_id);

CREATE INDEX idx_role_permissions_role_id ON role_permissions(role_id);
CREATE INDEX idx_role_permissions_permission_id ON role_permissions(permission_id);

-- User Logs Table → user_logs
CREATE TABLE user_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    detail TEXT,
    changes JSONB,
    action_type VARCHAR(20) NOT NULL CHECK (action_type IN ('Create', 'Update', 'Delete')),
    model_name VARCHAR(100) NOT NULL,
    model_id UUID,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ DEFAULT now(),
    created_at_id BIGINT
);

-- Trigger function to fill created_at_id
CREATE OR REPLACE FUNCTION set_created_at_id()
RETURNS TRIGGER AS $$
BEGIN
    NEW.created_at_id := 
        (EXTRACT(YEAR FROM NEW.created_at)::BIGINT * 10000000000) +
        (EXTRACT(MONTH FROM NEW.created_at)::BIGINT * 100000000) +
        (EXTRACT(DAY FROM NEW.created_at)::BIGINT * 1000000) +
        (EXTRACT(HOUR FROM NEW.created_at)::BIGINT * 10000) +
        (EXTRACT(MINUTE FROM NEW.created_at)::BIGINT * 100) +
        (EXTRACT(SECOND FROM NEW.created_at)::BIGINT);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Attach trigger
CREATE TRIGGER trg_set_created_at_id
BEFORE INSERT ON user_logs
FOR EACH ROW
EXECUTE FUNCTION set_created_at_id();

-- User Table Combinations → user_table_combinations
CREATE TABLE user_table_combinations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_id UUID NOT NULL,
    show_column_combinations TEXT[] NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    updated_by UUID REFERENCES users(id) ON DELETE SET NULL,
    updated_at TIMESTAMPTZ DEFAULT now()
);

-- Indexes
CREATE INDEX idx_user_table_combinations_user_id ON user_table_combinations(user_id);
CREATE INDEX idx_user_table_combinations_updated_by ON user_table_combinations(updated_by);

-- Attach existing trigger
CREATE TRIGGER trg_update_user_table_combinations
BEFORE UPDATE ON user_table_combinations
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();
