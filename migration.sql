CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `Cinemas` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nome` longtext NOT NULL,
    `Indirizzo` longtext NOT NULL,
    `Citta` longtext NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Registi` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nome` longtext NOT NULL,
    `Cognome` longtext NOT NULL,
    `Nazionalita` longtext NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Films` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Titolo` longtext NOT NULL,
    `DataProduzione` datetime(6) NOT NULL,
    `RegistaId` int NOT NULL,
    `Durata` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Films_Registi_RegistaId` FOREIGN KEY (`RegistaId`) REFERENCES `Registi` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `Proiezioni` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CinemaId` int NOT NULL,
    `FilmId` int NOT NULL,
    `Data` datetime(6) NOT NULL,
    `Ora` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Proiezioni_Cinemas_CinemaId` FOREIGN KEY (`CinemaId`) REFERENCES `Cinemas` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Proiezioni_Films_FilmId` FOREIGN KEY (`FilmId`) REFERENCES `Films` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_Films_RegistaId` ON `Films` (`RegistaId`);

CREATE UNIQUE INDEX `IX_Proiezioni_CinemaId_FilmId_Data_Ora` ON `Proiezioni` (`CinemaId`, `FilmId`, `Data`, `Ora`);

CREATE INDEX `IX_Proiezioni_FilmId` ON `Proiezioni` (`FilmId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260316101129_InitialCreate', '9.0.11');

COMMIT;

