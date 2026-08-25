import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { MockedObject } from 'vitest';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CandidateEducation } from '../../src/app/features/candidates/components/candidate-education';
import { CandidateExperience } from '../../src/app/features/candidates/components/candidate-experience';
import { CandidateSkills } from '../../src/app/features/candidates/components/candidate-skills';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';

describe('Candidate profile section components', () => {
  let relations: MockedObject<CandidateRelationsService>;

  const renderSection = (ui: React.ReactElement) =>
    render(
      <ServicesProvider
        value={{ ...services, candidateRelationsService: relations } as unknown as Services}
      >
        {ui}
      </ServicesProvider>,
    );

  beforeEach(() => {
    localStorage.clear();
    relations = {
      addEducation: vi.fn(),
      removeEducation: vi.fn(),
      addExperience: vi.fn(),
      removeExperience: vi.fn(),
      addSkill: vi.fn(),
      removeSkill: vi.fn(),
    } as unknown as MockedObject<CandidateRelationsService>;
  });

  describe('CandidateEducation', () => {
    it('surfaces the validation error from the relations service and keeps the draft', async () => {
      relations.addEducation.mockImplementation(() => {
        throw new Error('La titulación es obligatoria.');
      });
      renderSection(<CandidateEducation candidateId="c1" education={[]} canEdit />);

      await userEvent.type(screen.getByLabelText('Centro'), 'UCM');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(screen.getByText('La titulación es obligatoria.')).toBeInTheDocument();
      expect(screen.getByLabelText('Centro')).toHaveValue('UCM');
    });

    it('clears the draft and error after a successful add', async () => {
      renderSection(<CandidateEducation candidateId="c1" education={[]} canEdit />);

      await userEvent.type(screen.getByLabelText('Titulación'), 'Grado en ADE');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(relations.addEducation).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ degree: 'Grado en ADE' }),
      );
      expect(screen.queryByText(/obligatoria/)).not.toBeInTheDocument();
      expect(screen.getByLabelText('Titulación')).toHaveValue('');
    });
  });

  describe('CandidateExperience', () => {
    it('surfaces the date-order validation error', async () => {
      relations.addExperience.mockImplementation(() => {
        throw new Error('La fecha de fin no puede ser anterior a la fecha de inicio.');
      });
      renderSection(<CandidateExperience candidateId="c1" experience={[]} canEdit />);

      await userEvent.type(screen.getByLabelText('Fecha inicio'), '2024-06-01');
      await userEvent.type(screen.getByLabelText('Fecha fin'), '2024-01-01');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir experiencia' }));

      expect(
        screen.getByText('La fecha de fin no puede ser anterior a la fecha de inicio.'),
      ).toBeInTheDocument();
    });
  });

  describe('CandidateSkills', () => {
    it('surfaces the duplicate-skill validation error', async () => {
      relations.addSkill.mockImplementation(() => {
        throw new Error('El candidato ya tiene esta habilidad registrada.');
      });
      renderSection(<CandidateSkills candidateId="c1" skills={[]} canEdit />);

      await userEvent.click(screen.getByRole('button', { name: 'Añadir habilidad' }));

      expect(
        screen.getByText('El candidato ya tiene esta habilidad registrada.'),
      ).toBeInTheDocument();
    });

    it('removes a skill through the relations service', async () => {
      renderSection(
        <CandidateSkills
          candidateId="c1"
          skills={[{ id: 'skill-1', skill: 'Compras', level: 'Medio' }]}
          canEdit
        />,
      );

      await userEvent.click(screen.getByRole('button', { name: 'Quitar' }));

      expect(relations.removeSkill).toHaveBeenCalledWith('c1', 'skill-1');
    });
  });
});
