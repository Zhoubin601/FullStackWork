namespace ReadJournal.Library.Models.Reader;

public enum ReaderPhase { Idle, Opening, Presenting, Active, Closing, CloseFailed, OpenFailed }
public enum ReaderCloseDecision { Stay, Retry, ContinueReading }
