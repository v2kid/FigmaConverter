using System.Collections;

/// <summary>
/// Interface for Figma node converters that defines the contract for converting Figma data to UI elements
/// </summary>
public interface IFigmaNodeConverter
{
    /// <summary>
    /// Convert the assigned Figma node to UI elements
    /// </summary>
    void ConvertNodeToUI();

    /// <summary>
    /// Validate the converter setup and configuration
    /// </summary>
    void ValidateSetup();

    /// <summary>
    /// Clear any created UI elements
    /// </summary>
    void ClearCreatedUI();

    /// <summary>
    /// List all available nodes in the data asset
    /// </summary>
    void ListAvailableNodes();


    void GeneratePrefab();
}

/// <summary>
/// Interface specifically for UXML-based converters that can generate UXML and USS files
/// </summary>
public interface IFigmaUXMLConverter : IFigmaNodeConverter
{
    /// <summary>
    /// Generate UXML and USS files from Figma data
    /// </summary>
    void GenerateUXMLFile();

    /// <summary>
    /// Generate USS classes for styling
    /// </summary>
    void GenerateUSSClasses();
}