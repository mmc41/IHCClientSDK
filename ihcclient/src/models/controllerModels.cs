using System;

public record SDInfo {       
        public long Size { get; init; } 

        public long Free { get; init; } 
}        

public record BackupFile {
        // Hmm. Can't identify the file format.Does not seem to be compressed or encoded text. Raw Binary?
        public byte[] Data { get; init; } 
        
        public string Filename { get; init; }
}

public record ProjectInfo
{
        public int VisualMinorVersion { get; init; } 
        
        public int VisualMajorVersion { get; init; } 
        
        public int ProjectMajorRevision { get; init; } 
        
        public int ProjectMinorRevision { get; init; } 
        
        public DateTimeOffset? Lastmodified { get; init; } 
        
        public string ProjectNumber { get; init; } 
        
        public string CustomerName { get; init; } 
        
        public string InstallerName { get; init; }
}

public record ProjectFile {
        public string Data { get; init; } 
        
        public string Filename { get; init; }    
}