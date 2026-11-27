namespace Mau.Core.Entities;

/// <summary>The kind of ingredient, used for display and grouping.</summary>
public enum IngredientCategory { Protein, Vegetable, Aromatic, Seasoning, Liquid, Legume, Other }

/// <summary>How a dish is cooked.</summary>
public enum CookingMethod { Braised, Stewed, Soup, Fried, Grilled, Sauteed }

/// <summary>The language a synonym comes from.</summary>
public enum SynonymLanguage { English, Filipino }

/// <summary>The kind of feedback a user gives a dish.</summary>
public enum FeedbackType { Cooked, ThumbsUp, ThumbsDown }
