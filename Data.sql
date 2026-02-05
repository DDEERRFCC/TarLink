INSERT INTO users (Name, Email, PasswordHash, Role)
VALUES 
('Alice', 'alice@student.com', SHA2('password123', 256), 'Student'),
('Bob', 'bob@committee.com', SHA2('password123', 256), 'Committee'),
('Charlie', 'charlie@supervisor.com', SHA2('password123', 256), 'Supervisor');