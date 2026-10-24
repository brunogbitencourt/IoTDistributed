
CREATE TABLE IF NOT EXISTS sensors (
    sensor_id VARCHAR(50) PRIMARY KEY,
    sensor_type VARCHAR(50) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    owner_node_id INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sensor_readings (
    event_id UUID PRIMARY KEY,
    sensor_id VARCHAR(50) NOT NULL,
    value DOUBLE PRECISION NOT NULL,
    collected_at TIMESTAMPTZ NOT NULL,
    origin_node_id INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_sensor
        FOREIGN KEY (sensor_id)
        REFERENCES sensors(sensor_id)
);

CREATE INDEX IF NOT EXISTS idx_sensor_readings_sensor
ON sensor_readings(sensor_id, collected_at DESC);

INSERT INTO sensors
    (sensor_id, sensor_type, unit, owner_node_id)
VALUES
    ('TEMP-001', 'temperature', 'celsius', 1),
    ('PRESS-001', 'pressure', 'bar', 2),
    ('LEVEL-001', 'level', 'percent', 3)
ON CONFLICT (sensor_id) DO NOTHING;

