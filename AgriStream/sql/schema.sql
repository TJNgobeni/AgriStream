-- Run in Azure SQL Query editor (Task b)
CREATE TABLE Users (
    UserId       INT IDENTITY PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL CHECK (Role IN ('Farmer','Agronomist','Admin'))
);

CREATE TABLE Devices (
    DeviceId       NVARCHAR(50) PRIMARY KEY,
    FieldId        NVARCHAR(50) NOT NULL,
    Status         NVARCHAR(30) NOT NULL DEFAULT 'Idle',
    LastCommand    NVARCHAR(100) NULL,
    LastUpdatedUtc DATETIME2 NULL
);

INSERT INTO Devices (DeviceId, FieldId) VALUES
 ('sensor-001','field-A'),('sensor-002','field-A'),('sensor-003','field-A'),
 ('sensor-004','field-B'),('sensor-005','field-B'),('sensor-006','field-B'),
 ('sensor-007','field-C'),('sensor-008','field-C'),('sensor-009','field-C');
-- Users are created from the app's Register page (passwords are hashed).
