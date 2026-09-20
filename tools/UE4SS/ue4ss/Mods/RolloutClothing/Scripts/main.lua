-- Inject hoodie-navy-mod as a navy tint of the stock hoodie mesh.
local DT_PATH = "/Game/MainFolder/UI/customization/data/DT-upper.DT-upper"
local SRC_ROW = "hoodie-white"
local NEW_ROW = "hoodie-navy-mod"
local MALE_PROP = "UpperMale_29_F65309A84A25055308AE58A0E1B692CC"
local dumped = false
local injected = false
local attempts = 0

local function find_obj(path)
    local ok, obj = pcall(StaticFindObject, path)
    if ok and obj and obj:IsValid() then
        return obj
    end
    return nil
end

local function try_dump()
    if dumped then return end
    local ok, err = pcall(DumpUSMAP)
    if ok then
        dumped = true
        print("[RolloutClothing] DumpUSMAP ok")
    else
        print("[RolloutClothing] DumpUSMAP failed: " .. tostring(err))
    end
end

local function try_inject()
    if injected then return end
    local dt = find_obj(DT_PATH)
    if not dt then return end
    print("[RolloutClothing] found DT-upper")
    local existingOk, existing = pcall(function() return dt:FindRow(NEW_ROW) end)
    if existingOk and existing then
        print("[RolloutClothing] overlay already has row " .. NEW_ROW)
        pcall(function()
            local male = existing[MALE_PROP]
            if male and male:IsValid() then
                print("[RolloutClothing] UpperMale=" .. male:GetFullName())
            end
        end)
        injected = true
        return
    end
    local ok, src = pcall(function() return dt:FindRow(SRC_ROW) end)
    if not ok or not src then
        print("[RolloutClothing] missing row " .. SRC_ROW)
        injected = true
        return
    end
    local addOk, addErr = pcall(function() dt:AddRow(NEW_ROW, src) end)
    if addOk then
        print("[RolloutClothing] added row " .. NEW_ROW .. " (stock hoodie-white clone)")
    else
        print("[RolloutClothing] AddRow failed: " .. tostring(addErr))
    end
    injected = true
end

LoopAsync(1500, function()
    attempts = attempts + 1
    local ok, err = pcall(function()
        try_dump()
        try_inject()
    end)
    if not ok then
        print("[RolloutClothing] tick error: " .. tostring(err))
    end
    return (dumped and injected) or attempts > 20
end)

print("[RolloutClothing] loaded")
