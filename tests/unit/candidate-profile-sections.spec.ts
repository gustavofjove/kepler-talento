import { TestBed } from '@angular/core/testing';
import { CandidateEducationComponent } from '../../src/app/features/candidates/components/candidate-education.component';
import { CandidateExperienceComponent } from '../../src/app/features/candidates/components/candidate-experience.component';
import { CandidateSkillsComponent } from '../../src/app/features/candidates/components/candidate-skills.component';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';

describe('Candidate profile section components', () => {
  let relations: jest.Mocked<CandidateRelationsService>;

  beforeEach(() => {
    relations = {
      addEducation: jest.fn(),
      removeEducation: jest.fn(),
      addExperience: jest.fn(),
      removeExperience: jest.fn(),
      addSkill: jest.fn(),
      removeSkill: jest.fn(),
    } as unknown as jest.Mocked<CandidateRelationsService>;
  });

  describe('CandidateEducationComponent', () => {
    it('surfaces the validation error from the relations service and keeps the draft', () => {
      relations.addEducation.mockImplementation(() => {
        throw new Error('La titulación es obligatoria.');
      });
      TestBed.configureTestingModule({
        imports: [CandidateEducationComponent],
        providers: [{ provide: CandidateRelationsService, useValue: relations }],
      });
      const fixture = TestBed.createComponent(CandidateEducationComponent);
      fixture.componentInstance.candidateId = 'c1';
      fixture.componentInstance.draft.institution = 'UCM';

      fixture.componentInstance.add();

      expect(fixture.componentInstance.error).toBe('La titulación es obligatoria.');
      expect(fixture.componentInstance.draft.institution).toBe('UCM');
    });

    it('clears the draft and error after a successful add', () => {
      TestBed.configureTestingModule({
        imports: [CandidateEducationComponent],
        providers: [{ provide: CandidateRelationsService, useValue: relations }],
      });
      const fixture = TestBed.createComponent(CandidateEducationComponent);
      fixture.componentInstance.candidateId = 'c1';
      fixture.componentInstance.draft.degree = 'Grado en ADE';

      fixture.componentInstance.add();

      expect(relations.addEducation).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ degree: 'Grado en ADE' }),
      );
      expect(fixture.componentInstance.error).toBe('');
      expect(fixture.componentInstance.draft.degree).toBe('');
    });
  });

  describe('CandidateExperienceComponent', () => {
    it('surfaces the date-order validation error', () => {
      relations.addExperience.mockImplementation(() => {
        throw new Error('La fecha de fin no puede ser anterior a la fecha de inicio.');
      });
      TestBed.configureTestingModule({
        imports: [CandidateExperienceComponent],
        providers: [{ provide: CandidateRelationsService, useValue: relations }],
      });
      const fixture = TestBed.createComponent(CandidateExperienceComponent);
      fixture.componentInstance.candidateId = 'c1';
      fixture.componentInstance.draft.startDate = '2024-06-01';
      fixture.componentInstance.draft.endDate = '2024-01-01';

      fixture.componentInstance.add();

      expect(fixture.componentInstance.error).toBe(
        'La fecha de fin no puede ser anterior a la fecha de inicio.',
      );
    });
  });

  describe('CandidateSkillsComponent', () => {
    it('surfaces the duplicate-skill validation error', () => {
      relations.addSkill.mockImplementation(() => {
        throw new Error('El candidato ya tiene esta habilidad registrada.');
      });
      TestBed.configureTestingModule({
        imports: [CandidateSkillsComponent],
        providers: [{ provide: CandidateRelationsService, useValue: relations }],
      });
      const fixture = TestBed.createComponent(CandidateSkillsComponent);
      fixture.componentInstance.candidateId = 'c1';
      fixture.componentInstance.draft = { skill: 'Compras', level: 'Medio' };

      fixture.componentInstance.add();

      expect(fixture.componentInstance.error).toBe(
        'El candidato ya tiene esta habilidad registrada.',
      );
    });

    it('removes a skill through the relations service', () => {
      TestBed.configureTestingModule({
        imports: [CandidateSkillsComponent],
        providers: [{ provide: CandidateRelationsService, useValue: relations }],
      });
      const fixture = TestBed.createComponent(CandidateSkillsComponent);
      fixture.componentInstance.candidateId = 'c1';

      fixture.componentInstance.remove('skill-1');

      expect(relations.removeSkill).toHaveBeenCalledWith('c1', 'skill-1');
    });
  });
});
