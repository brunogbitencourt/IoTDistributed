
ALTER TABLE sensors
ADD COLUMN IF NOT EXISTS mqtt_topic VARCHAR(255);

UPDATE sensors
SET mqtt_topic = CASE sensor_id
    WHEN 'TEMP-001'
        THEN 'iot/sensors/TEMP-001/readings'

    WHEN 'PRESS-001'
        THEN 'iot/sensors/PRESS-001/readings'

    WHEN 'LEVEL-001'
        THEN 'iot/sensors/LEVEL-001/readings'
END
WHERE mqtt_topic IS NULL
  AND sensor_id IN (
      'TEMP-001',
      'PRESS-001',
      'LEVEL-001'
  );
