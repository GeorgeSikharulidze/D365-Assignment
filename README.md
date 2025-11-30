## 1. User Information - Age Calculation

**File:** `FormScript.js`

Automatically calculates a person's age based on their birth date.

**How it works:**
- Runs on form load
- Triggers on Birth Date field change
- Calculates age and sets the read-only Age field

**Key Functions:**
```javascript
GS.formOnLoad(executionContext)  // Entry point
calculateAge(formContext)         // Age calculation logic
```

---

## 2. Account Address Sync Plugin

**File:** `AccountAddressSyncPlugin.cs`

Synchronizes the Custom Account Address field from Account to all related Contacts.

**Relationship:**
```
┌─────────────┐         ┌─────────────┐
│   ACCOUNT   │ 1     N │   CONTACT   │
├─────────────┤────────►├─────────────┤
│ CustomAddr  │         │ CustomAddr  │
└─────────────┘         └─────────────┘
```

**Trigger:** Account Update (Post-Operation)

**Logic:**
1. Account's CustomAccountAddress field is updated
2. Plugin queries all Contacts linked to this Account
3. Updates each Contact's CustomAccountAddress field

---

## 3. Registration Materials Plugin

**File:** `RegistrationMaterialsPlugin.cs`

Automatically populates required materials when a student registers for a course.

**Relationships:**
```
┌──────────┐     ┌──────────┐     ┌──────────┐
│ STUDENT  │     │  COURSE  │     │ MATERIAL │
└────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │
     │    N     1     │     1     N    │
     └───────►┌───────┴────────┐◄──────┘
              │ REGISTRATION   │
              ├────────────────┤
              │ MaterialsNeeded│ ← Plugin populates
              └────────────────┘
```

**Trigger:** Registration Create (Post-Operation)

**Logic:**
1. User creates Registration (selects Student + Course)
2. Plugin queries all Materials linked to selected Course
3. Concatenates material names into "Materials Needed" field

**Example Output:**
```
Materials Needed: "Laptop, Python Textbook, VS Code Software"
```

---

## 4. Grade & Credits Calculation Plugin

**File:** `GradeCreditsCalculationPlugin.cs`

Calculates pass/fail status, credits earned, and GPA when grades are entered.

**Trigger:** Registration Create & Update (Post-Operation)

**Logic:**
1. User enters Grade (0-100) on Registration
2. Plugin determines if student passed (Grade ≥ 51)
3. If passed → Awards course credits
4. Calculates weighted GPA across all registrations
5. Updates Student's Total Credits and GPA

**GPA Scale (4.0):**

| Grade | GPA Points |
|-------|------------|
| 90-100 | 4.0 |
| 80-89 | 3.0 |
| 70-79 | 2.0 |
| 60-69 | 1.0 |
| 51-59 | 0.5 |
| 0-50 | 0.0 |

**GPA Formula:**
```
GPA = Σ(GPA Points × Course Credits) / Total Credits Attempted
```

**Example:**

| Course | Credits | Grade | GPA Points | Weighted |
|--------|---------|-------|------------|----------|
| Programming | 3 | 85 | 3.0 | 9.0 |
| Database | 4 | 72 | 2.0 | 8.0 |
| Web Dev | 5 | 95 | 4.0 | 20.0 |
| **Total** | **12** | | | **37.0** |

**GPA = 37.0 / 12 = 3.08**

---

## Plugin Registration Summary

| Plugin | Message | Entity | Stage |
|--------|---------|--------|-------|
| AccountAddressSyncPlugin | Update | account | Post-Operation |
| RegistrationMaterialsPlugin | Create | gs_registrations | Post-Operation |
| GradeCreditsCalculationPlugin | Create | gs_registrations | Post-Operation |
| GradeCreditsCalculationPlugin | Update | gs_registrations | Post-Operation |