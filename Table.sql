-- ==============================
-- USE DATABASE
-- ==============================
USE internship_db;
-- ==============================
-- DROP ALL TABLES (SAFE ORDER)
-- ==============================
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS companyrequest,
studentcv,
studentapplication,
studentstatus,
studentaccess,
progressreport,
pullback,
appointmentlettertemplate,
blacklistcompany,
cohort,
company,
person,
sysuser,
sysconfig,
ucsupervisor;
SET FOREIGN_KEY_CHECKS = 1;
-- ==============================
-- PERSON TABLE
-- ==============================
CREATE TABLE person (
    person_id INT AUTO_INCREMENT PRIMARY KEY,
    full_name VARCHAR(100) NOT NULL,
    email VARCHAR(100) UNIQUE,
    phone VARCHAR(20),
    role VARCHAR(30),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
-- ==============================
-- COMPANY TABLE
-- ==============================
CREATE TABLE company (
    company_id INT AUTO_INCREMENT PRIMARY KEY,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    lastUpdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    lastVisit DATE,
    lastContact DATE,
    regNo VARCHAR(15),
    vacancyLevel VARCHAR(15),
    name VARCHAR(250) NOT NULL,
    address1 VARCHAR(255),
    address2 VARCHAR(255),
    address3 VARCHAR(255),
    totalNoOfStaff INT,
    industryInvolved VARCHAR(150),
    productsAndServices VARCHAR(150),
    companyBackground VARCHAR(255),
    logo BLOB,
    website VARCHAR(100),
    ssmCert LONGBLOB,
    status TINYINT DEFAULT 1,
    visibility TINYINT(1) DEFAULT 1,
    remark VARCHAR(500),
    INDEX idx_regNo (regNo),
    INDEX idx_status (status),
    INDEX idx_name (name(100))
);
-- ==============================
-- BLACKLIST COMPANY TABLE
-- ==============================
CREATE TABLE blacklistcompany (
    id INT AUTO_INCREMENT PRIMARY KEY,
    created_at TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
    comName VARCHAR(255),
    address VARCHAR(500),
    reason VARCHAR(500),
    byCommittee VARCHAR(255),
    campus VARCHAR(45),
    faculty VARCHAR(45),
    attachment VARCHAR(255)
);
-- ==============================
-- COHORT TABLE
-- ==============================
CREATE TABLE cohort (
    cohort_id INT AUTO_INCREMENT PRIMARY KEY,
    description VARCHAR(100),
    startDate DATE,
    endDate DATE,
    level TINYINT,
    isActive TINYINT(1),
    report1DueDate DATE,
    report2DueDate DATE,
    report3DueDate DATE,
    report4DueDate DATE,
    report5DueDate DATE,
    finalReportDueDate DATE,
    examStartDate DATE,
    examEndDate DATE,
    companyEvaluationDate DATE,
    reportMonth1 VARCHAR(45),
    reportMonth2 VARCHAR(45),
    reportMonth3 VARCHAR(45),
    reportMonth4 VARCHAR(45),
    reportMonth5 VARCHAR(45),
    reportMonth6 VARCHAR(45),
    campus VARCHAR(45),
    faculty VARCHAR(45),
    personInCharge VARCHAR(250),
    pidEmail VARCHAR(250),
    INDEX idx_startDate (startDate),
    INDEX idx_endDate (endDate),
    INDEX idx_isActive (isActive),
    INDEX idx_campus (campus),
    INDEX idx_faculty (faculty)
);
-- ==============================
-- COMPANY REQUEST TABLE
-- ==============================
CREATE TABLE companyrequest (
    request_id INT AUTO_INCREMENT PRIMARY KEY,
    company_id INT NOT NULL,
    request_type VARCHAR(50),
    status VARCHAR(30),
    requested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_companyrequest_company FOREIGN KEY (company_id) REFERENCES company(company_id)
);
-- ==============================
-- APPOINTMENT LETTER TEMPLATE
-- ==============================
CREATE TABLE appointmentlettertemplate (
    template_id INT AUTO_INCREMENT PRIMARY KEY,
    template_name VARCHAR(100),
    content TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
-- ==============================
-- STUDENT CV TABLE
-- ==============================
CREATE TABLE studentcv (
    cv_id INT AUTO_INCREMENT PRIMARY KEY,
    student_id INT NOT NULL,
    file_path VARCHAR(255),
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_studentcv_person FOREIGN KEY (student_id) REFERENCES person(person_id)
);
-- ==============================
-- STUDENT APPLICATION TABLE
-- ==============================
-- Example of improved structure
CREATE TABLE studentapplication (
    application_id INT AUTO_INCREMENT PRIMARY KEY,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    studentID VARCHAR(45) NOT NULL UNIQUE,
    studentName VARCHAR(255) NOT NULL,
    gender ENUM('M', 'F', 'O') DEFAULT 'O',
    studentEmail VARCHAR(255) NOT NULL,
    contactDate DATE,
    comName VARCHAR(255),
    comAddress VARCHAR(500),
    comSupervisor VARCHAR(255),
    comSupervisorEmail VARCHAR(255),
    comSupervisorContact VARCHAR(255),
    allowance DECIMAL(10, 2),
    ucSupervisor VARCHAR(255),
    ucSupervisorEmail VARCHAR(255),
    ucSupervisorContact VARCHAR(255),
    applyStatus ENUM('pending', 'approved', 'rejected', 'withdrawn') DEFAULT 'pending',
    remark TEXT,
    level TINYINT,
    cohortId INT NOT NULL,
    programme VARCHAR(4),
    groupNo INT,
    CGPA DECIMAL(3, 2),
    ownTransport TINYINT(1) DEFAULT 0,
    tempAddress VARCHAR(500),
    personalEmail VARCHAR(255),
    permanentAddress TEXT,
    permanentContact VARCHAR(45),
    healthRemark TEXT,
    programmingKnowledge VARCHAR(50),
    databaseKnowledge VARCHAR(50),
    networkingKnowledge VARCHAR(50),
    templateVersion TINYINT DEFAULT 1,
    formAcceptance VARCHAR(255),
    formAcknowledgement VARCHAR(255),
    letterIdentity VARCHAR(255),
    otherEvidence VARCHAR(255),
    doVerifier VARCHAR(255),
    doVerifierEmail VARCHAR(255),
    isAgreed TINYINT(1) DEFAULT 0,
    INDEX idx_studentID (studentID),
    INDEX idx_cohortId (cohortId),
    INDEX idx_applyStatus (applyStatus),
    INDEX idx_studentEmail (studentEmail),
    INDEX idx_personalEmail (personalEmail),
    INDEX idx_comName (comName),
    CONSTRAINT fk_studentapplication_cohort FOREIGN KEY (cohortId) REFERENCES cohort(cohort_id) ON DELETE RESTRICT ON UPDATE CASCADE
);
-- ==============================
-- STUDENT STATUS TABLE
-- ==============================
CREATE TABLE studentstatus (
    status_id INT AUTO_INCREMENT PRIMARY KEY,
    student_id INT NOT NULL,
    status VARCHAR(50),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_studentstatus_person FOREIGN KEY (student_id) REFERENCES person(person_id)
);
-- ==============================
-- STUDENT ACCESS TABLE
-- ==============================
CREATE TABLE studentaccess (
    access_id INT AUTO_INCREMENT PRIMARY KEY,
    student_id INT NOT NULL,
    access_level VARCHAR(30),
    granted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_studentaccess_person FOREIGN KEY (student_id) REFERENCES person(person_id)
);
-- ==============================
-- PROGRESS REPORT TABLE
-- ==============================
CREATE TABLE progressreport (
    report_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    timeStamp DATETIME,
    lastUpdate DATETIME,
    applicantId BIGINT,
    cohortId INT,
    month VARCHAR(50),
    dueDate DATE,
    status TINYINT,
    remark LONGTEXT,
    CONSTRAINT fk_progressreport_cohort FOREIGN KEY (cohortId) REFERENCES cohort(cohort_id)
);
-- ==============================
-- PULLBACK TABLE
-- ==============================
CREATE TABLE pullback (
    pullback_id INT AUTO_INCREMENT PRIMARY KEY,
    timeStamp DATETIME,
    studentId VARCHAR(10),
    cohortId INT,
    reason VARCHAR(50),
    updatedBy VARCHAR(255),
    attachment VARCHAR(255),
    CONSTRAINT fk_pullback_cohort FOREIGN KEY (cohortId) REFERENCES cohort(cohort_id)
);
-- ==============================
-- SYSTEM USER TABLE
-- ==============================
CREATE TABLE sysuser (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role ENUM('committee', 'supervisor', 'student') NOT NULL DEFAULT 'student',
    ic_number VARCHAR(20) UNIQUE NULL,
    -- Changed to allow NULL
    application_id INT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    is_locked BOOLEAN DEFAULT FALSE,
    login_attempts TINYINT DEFAULT 0,
    last_login_at TIMESTAMP NULL,
    password_changed_at TIMESTAMP NULL,
    password_reset_token VARCHAR(100) NULL,
    password_reset_expires TIMESTAMP NULL,
    email_verified_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    -- Essential indexes (avoiding redundancy)
    INDEX idx_role (role),
    INDEX idx_application_id (application_id),
    INDEX idx_is_active (is_active),
    INDEX idx_is_locked (is_locked),
    -- Added for admin queries
    -- Foreign key constraint
    CONSTRAINT fk_sysuser_studentapplication FOREIGN KEY (application_id) REFERENCES studentapplication(application_id) ON DELETE
    SET NULL ON UPDATE CASCADE
);
-- Optional: Add a trigger to enforce role-application_id consistency
DELIMITER // CREATE TRIGGER check_application_id_role_update BEFORE
UPDATE ON sysuser FOR EACH ROW BEGIN IF NEW.role != 'student'
    AND NEW.application_id IS NOT NULL THEN SIGNAL SQLSTATE '45000'
SET MESSAGE_TEXT = 'Only students can have an application_id';
END IF;
END;
studentapplication DELIMITER;
-- ==============================
-- SYSTEM CONFIG TABLE
-- ==============================
CREATE TABLE sysconfig (
    config_id INT AUTO_INCREMENT PRIMARY KEY,
    config_key VARCHAR(100) UNIQUE,
    config_value TEXT
);
-- ==============================
-- UC SUPERVISOR TABLE
-- ==============================
CREATE TABLE ucsupervisor (
    staffId VARCHAR(16) PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    email VARCHAR(250) UNIQUE,
    password VARCHAR(255),
    remark VARCHAR(150),
    isActive TINYINT(1) DEFAULT 1,
    isCommittee TINYINT(1) DEFAULT 0,
    faculty VARCHAR(45),
    campus VARCHAR(45),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);