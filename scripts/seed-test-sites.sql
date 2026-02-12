-- Test data for Sites table
-- Run this script to populate the database with sample sites for testing
-- Note: This will insert sites with various characteristics to test all filters

-- Clear existing sites (optional - comment out if you want to keep existing data)
-- DELETE FROM "Sites";

-- Insert diverse test sites
INSERT INTO "Sites" ("Domain", "DR", "Traffic", "Location", "PriceUsd", "PriceCasino", "PriceCrypto", "PriceLinkInsert", "Niche", "Categories", "IsQuarantined", "QuarantineReason", "CreatedAtUtc", "UpdatedAtUtc") VALUES
-- High-value available sites
('techblog.com', 85, 2500000, 'US', 350, 450, 400, 180, 'Technology', 'Tech, AI, Software', false, null, NOW(), NOW()),
('healthhub.com', 78, 1800000, 'US', 280, 320, 300, 150, 'Health', 'Wellness, Nutrition', false, null, NOW(), NOW()),
('financenews.com', 82, 2200000, 'UK', 400, null, 380, 200, 'Finance', 'Investment, Banking', false, null, NOW(), NOW()),
('travelguide.net', 70, 1500000, 'CA', 220, 280, null, 120, 'Travel', 'Tourism, Hotels', false, null, NOW(), NOW()),
('sportsfanatic.com', 75, 1900000, 'US', 300, 350, 320, 160, 'Sports', 'NFL, NBA, Soccer', false, null, NOW(), NOW()),

-- Mid-range sites with various pricing
('cookingbasics.com', 55, 850000, 'UK', 150, 180, null, 80, 'Food', 'Recipes, Cooking', false, null, NOW(), NOW()),
('petcare101.com', 52, 720000, 'AU', 130, null, null, 70, 'Pets', 'Dogs, Cats', false, null, NOW(), NOW()),
('gardentips.net', 48, 620000, 'CA', 110, null, null, 60, 'Gardening', 'Plants, DIY', false, null, NOW(), NOW()),
('fashiontrends.com', 65, 1200000, 'US', 200, 240, 220, 100, 'Fashion', 'Style, Trends', false, null, NOW(), NOW()),
('autoexpert.com', 60, 950000, 'DE', 180, null, 170, 90, 'Automotive', 'Cars, Reviews', false, null, NOW(), NOW()),

-- Lower DR sites
('budgetliving.com', 35, 420000, 'US', 80, null, null, 40, 'Lifestyle', 'Frugal, Tips', false, null, NOW(), NOW()),
('techstartup.io', 42, 580000, 'US', 100, null, 95, 50, 'Startups', 'Tech, Business', false, null, NOW(), NOW()),
('homeschool.net', 38, 350000, 'CA', 70, null, null, 35, 'Education', 'Learning, Kids', false, null, NOW(), NOW()),
('localfood.org', 30, 280000, 'UK', 60, null, null, 30, 'Food', 'Organic, Local', false, null, NOW(), NOW()),
('craftideas.com', 33, 310000, 'AU', 65, null, null, 32, 'Crafts', 'DIY, Handmade', false, null, NOW(), NOW()),

-- Sites with casino pricing
('gamereview.com', 68, 1100000, 'US', 190, 280, null, 95, 'Gaming', 'Reviews, Guides', false, null, NOW(), NOW()),
('nightlife.net', 58, 780000, 'UK', 140, 220, null, 75, 'Entertainment', 'Clubs, Events', false, null, NOW(), NOW()),
('luxurylifestyle.com', 72, 1400000, 'US', 250, 340, 300, 130, 'Luxury', 'High-end, Premium', false, null, NOW(), NOW()),

-- Sites with crypto pricing
('cryptoinsider.com', 76, 1650000, 'US', 280, 320, 380, 140, 'Cryptocurrency', 'Bitcoin, Blockchain', false, null, NOW(), NOW()),
('defiworld.io', 62, 920000, 'US', 170, null, 250, 85, 'Finance', 'DeFi, Crypto', false, null, NOW(), NOW()),
('nftmarketplace.com', 58, 810000, 'UK', 150, 180, 220, 75, 'NFT', 'Digital Art', false, null, NOW(), NOW()),

-- Sites with all pricing types
('onlinecasino.review', 64, 1050000, 'US', 180, 260, 240, 90, 'Gaming', 'Casino, Gambling', false, null, NOW(), NOW()),
('investmentpro.com', 80, 1950000, 'UK', 320, 380, 420, 160, 'Finance', 'Investing, Trading', false, null, NOW(), NOW()),

-- Quarantined sites with various reasons
('scamsalert.net', 45, 520000, 'US', 90, null, null, 45, 'Security', 'Scams, Fraud', true, 'Low-quality content reported by multiple clients', NOW(), NOW()),
('cheaplinks.com', 28, 180000, 'Unknown', 40, null, null, 20, 'Various', 'Link Farm', true, 'Suspected link farm - under review', NOW(), NOW()),
('copycontent.org', 32, 250000, 'US', 50, null, null, 25, 'General', 'Mixed', true, 'Duplicate content detected', NOW(), NOW()),
('spammy-site.net', 22, 120000, 'Unknown', 30, null, null, 15, null, null, true, 'Multiple spam complaints', NOW(), NOW()),
('expired-domain.com', 55, 650000, 'UK', 120, null, null, 60, 'Technology', 'Software', true, 'Domain ownership dispute', NOW(), NOW()),

-- More diverse sites
('musicnews.com', 67, 1150000, 'US', 195, 230, null, 98, 'Music', 'News, Reviews', false, null, NOW(), NOW()),
('moviecritics.net', 71, 1320000, 'UK', 240, 280, null, 120, 'Entertainment', 'Movies, TV', false, null, NOW(), NOW()),
('businessinsider.io', 84, 2300000, 'US', 380, null, 360, 190, 'Business', 'News, Analysis', false, null, NOW(), NOW()),
('scienceworld.org', 73, 1450000, 'CA', 260, null, null, 130, 'Science', 'Research, News', false, null, NOW(), NOW()),
('parentingtips.com', 50, 680000, 'US', 125, null, null, 62, 'Parenting', 'Kids, Family', false, null, NOW(), NOW()),

-- Edge cases for testing
('example.com', 50, 500000, 'US', 100, 120, 110, 50, 'Example', 'Test', false, null, NOW(), NOW()),
('www-test.net', 45, 450000, 'CA', 90, null, null, 45, 'Testing', 'Demo', false, null, NOW(), NOW()),
('high-traffic.com', 88, 5000000, 'US', 500, 600, 550, 250, 'News', 'Breaking News', false, null, NOW(), NOW()),
('low-traffic.org', 25, 50000, 'UK', 30, null, null, 15, 'Blog', 'Personal', false, null, NOW(), NOW()),

-- International locations
('deutschland-news.de', 66, 1080000, 'DE', 185, 220, null, 92, 'News', 'Germany, Europe', false, null, NOW(), NOW()),
('aussie-sports.com.au', 59, 820000, 'AU', 145, 175, null, 72, 'Sports', 'Cricket, Rugby', false, null, NOW(), NOW()),
('canada-today.ca', 63, 970000, 'CA', 165, null, 155, 82, 'News', 'Canadian News', false, null, NOW(), NOW()),

-- Sites with no optional pricing
('minimalist-blog.com', 40, 390000, 'US', 75, null, null, null, 'Lifestyle', 'Minimalism', false, null, NOW(), NOW()),
('opensource-dev.org', 47, 560000, 'US', 95, null, null, null, 'Technology', 'Open Source', false, null, NOW(), NOW());

-- Display summary
SELECT 
    COUNT(*) as "Total Sites",
    COUNT(*) FILTER (WHERE "IsQuarantined" = true) as "Quarantined",
    COUNT(*) FILTER (WHERE "IsQuarantined" = false) as "Available",
    COUNT(DISTINCT "Location") as "Unique Locations",
    MIN("DR") as "Min DR",
    MAX("DR") as "Max DR",
    AVG("DR")::numeric(10,2) as "Avg DR"
FROM "Sites";

-- Show location distribution
SELECT 
    "Location",
    COUNT(*) as "Count"
FROM "Sites"
GROUP BY "Location"
ORDER BY "Count" DESC;
